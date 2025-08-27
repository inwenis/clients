module PGNIG

open PuppeteerSharp
open Utils


type InvoiceData = {
    BeforeClickingOnInvoice: string list
    AfterClickingOnInvoice: string list
}

type PGNiGClient(username, password, args, ?page : IPage, ?isSignedIn, ?isTest) =
    let isTest = isTest |> Option.defaultValue true
    let p, isSignedIn =
        match page, isSignedIn with
        | Some p, Some s     -> p, s
        | Some p, None       -> p, true
        | None,   Some false -> null, false
        | None,   Some true  -> failwith "You can not be signed in if you don't give me a page"
        | None,   None       -> null, false

    let mutable signedIn = isSignedIn
    let mutable p : IPage = p

    let getPage () =
        // let bf = new BrowserFetcher()
        let opt = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions(), Args = args)
        // bf.DownloadAsync() |> wait
        let brw = Puppeteer.LaunchAsync opt |> runSync
        brw.PagesAsync() |> runSync |> Array.exactlyOne

    let getPageIfNull () =
        if p = null then
            p <- getPage()
        p

    let signInInternal () =
        let w = p.WaitForNetworkIdleAsync()
        p.GoToAsync "https://ebok.pgnig.pl/" |> wait
        printf "Waiting for page to load... "
        w |> wait
        printfn "done"
        click p "xpath///button[text()='Odrzuć wszystkie']"
        sleep 1
        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        typet p "xpath///input[@name='identificator']" (username ())
        typet p "xpath///input[@name='accessPin']" (password ())
        let w = p.WaitForNetworkIdleAsync()
        sleep 1 // I have experienced that without waiting here clicking the "submit" button has no effect
        click p "xpath///button[@type='submit']"
        printf "Waiting for page to load... "
        w |> wait
        printfn "done"
        signedIn <- true

    member this.SignIn() =
        p <- getPageIfNull()
        if signedIn |> not then
            signInInternal()

    member this.SubmitIndication(indication) =
        p.GoToAsync "https://ebok.pgnig.pl/odczyt" |> wait

        printfn "Waiting for page to load... "
        waitTillHTMLRendered p
        printfn "done"

        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1

        p.TypeAsync("xpath///input[@id='reading-0']", indication |> string) |> wait

        click p "xpath///button[contains(text(), 'Zapisz odczyt')]"

        click p "xpath///button[contains(text(), 'Tak')]"
        // make sure the input was accepted
        printfn "Waiting for page to load... "
        waitTillHTMLRendered p
        printfn "done"

    member this.ScrapeInvoicesInternal() =
        // we rely on the index here because the list is rebuild and DOM nodes are detached
        let invoice_indexes =
            p.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result
            |> Array.mapi (fun i _ -> i)

        let invoices = [
            for index in invoice_indexes do
                let invoice_row =
                    p.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result
                    |> Array.mapi (fun i x -> i, x)
                    |> Array.find (fun (i,_) -> i = index)
                    |> snd

                // the "amount to pay is only available in the table before clicking on a invoice"
                let invoice_row_cells = invoice_row.QuerySelectorAllAsync("xpath/./div/div").Result
                invoice_row.QuerySelectorAsync("xpath/.//i[contains(@class,'icon-zoom')]").Result.ClickAsync().Wait() // click the magnifier to show details of the invoice

                // some invoices take long to load
                let mutable details_loaded = false
                while details_loaded |> not do
                    // wait to avoid busy waiting
                    // wait before the first check as querying for the modal immediately after clicking the magnifier will return null
                    sleep 1
                    let details_text = p.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result.GetPropertyAsync("textContent").Result |> string // get all the text of the modal that displays the invoice's details
                    if details_text.Contains("Numer faktury") then details_loaded <- true
                    if details_text.Contains("Numer noty") then details_loaded <- true

                let modal = p.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result
                let modal_rows = modal.QuerySelectorAllAsync("xpath/./div[@class='agreementModal']/div").Result

                p.Keyboard.PressAsync("Escape").Wait() // press Escape so we can get details for next invoice
                sleep 2
                yield invoice_row_cells, modal_rows ]

        invoices

    member this.ScrapeInvoices() =
        let getAllTexts (x:IElementHandle) =
            x.QuerySelectorAllAsync("xpath/.//text()").Result
            |> Array.map (fun x -> x.GetPropertyAsync("textContent").Result.JsonValueAsync().Result |> string)
            |> List.ofArray

        let parseInvoiceToStrings (before : IElementHandle array, after : IElementHandle array) =
            let before = before |> List.ofArray |> List.collect getAllTexts |> List.map (fun x -> x.Trim()) |> List.filter (fun x -> x <> "")
            let after  = after  |> List.ofArray |> List.collect getAllTexts |> List.map (fun x -> x.Trim()) |> List.filter (fun x -> x <> "")
            {
                BeforeClickingOnInvoice = before
                AfterClickingOnInvoice = after
            }

        // await page.evaluate(() => document.body.style.zoom = 0.5  );

        p.EvaluateExpressionAsync("() => document.body.style.zoom = 0.5").Wait()

        p.GoToAsync "https://ebok.pgnig.pl/faktury" |> Async.AwaitTask |> Async.RunSynchronously |> ignore
        sleep 2
        let invoices = this.ScrapeInvoicesInternal()

        invoices
        |> List.map parseInvoiceToStrings

    member this.ScrapeOverpayments() =
        p.GoToAsync "https://ebok.pgnig.pl/umowy" |> Async.AwaitTask |> Async.RunSynchronously |> ignore
        sleep 2

        let rows = p.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result

        rows
        |> List.ofArray
        |> List.map (fun row ->
            row.QuerySelectorAllAsync("xpath/.//div[contains(@class,'columns')]").Result
            |> List.ofArray
            |> List.map (fun cell -> cell.EvaluateFunctionAsync<string>("el => el.textContent").Result)
            |> List.map (fun x -> x.Trim())
            |> List.filter (fun x -> x <> "") )

    member this.GetP() = p

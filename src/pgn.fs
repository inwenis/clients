module PGNIG

open PuppeteerSharp
open Utils


type InvoiceData = {
    BeforeClickingOnInvoice: string list
    AfterClickingOnInvoice: string list
}

type PGNiGClient(username, password, ?args, ?page : IPage, ?isSignedIn, ?isTest) =
    let isTest = isTest |> Option.defaultValue true
    let p, isSignedIn =
        match page, isSignedIn with
        | Some p, Some s     -> p, s
        | Some p, None       -> p, true
        | None,   Some false -> null, false
        | None,   Some true  -> failwith "You can not be signed in if you don't give me a page"
        | None,   None       -> null, false

    let args = args |> Option.defaultValue [||]

    let mutable signedIn = isSignedIn
    let mutable p : IPage = p

    do downloadDefaultBrowser ()

    let signInInternal () =
        let w = p.WaitForNetworkIdleAsync()
        goto p "https://ebok.pgnig.pl/"
        w |> wait
        click p "xpath///button[text()='Odrzuć wszystkie']"
        sleep 1
        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        typet p "xpath///input[@name='identificator']" (username ())
        typet p "xpath///input[@name='accessPin']" (password ())
        let w = p.WaitForNetworkIdleAsync()
        sleep 1 // I have experienced that without waiting here clicking the "submit" button has no effect
        click p "xpath///button[@type='submit']"
        w |> wait
        signedIn <- true

    let ScrapeInvoicesInternal () =
        // we rely on the index here because the list is rebuild and DOM nodes are detached
        let invoice_indexes = queryAll p "xpath///div[contains(@class,'table-row')]" |> Array.mapi (fun i _ -> i)

        let invoices = [
            for index in invoice_indexes do
                let invoice_row =
                    queryAll p "xpath///div[contains(@class,'table-row')]"
                    |> Array.mapi (fun i x -> i, x)
                    |> Array.find (fun (i,_) -> i = index)
                    |> snd

                // the "amount to pay is only available in the table before clicking on a invoice"
                let invoice_row_cells = queryElementAll invoice_row "xpath/./div/div"
                clickSelector "xpath/.//i[contains(@class,'icon-zoom')]" invoice_row // click the magnifier to show details of the invoice

                // some invoices take long to load
                let mutable details_loaded = false
                while details_loaded |> not do
                    // wait to avoid busy waiting
                    // wait before the first check as querying for the modal immediately after clicking the magnifier will return null
                    sleep 1
                    let details_text =
                        querySingle p "xpath///div[@class='ModalContent']"
                        |> fun x -> x.GetPropertyAsync("textContent").Result
                        |> string // get all the text of the modal that displays the invoice's details
                    if details_text.Contains("Numer faktury") then details_loaded <- true
                    if details_text.Contains("Numer noty") then details_loaded <- true

                let modal = querySingle p "xpath///div[@class='ModalContent']"
                let modal_rows = queryElementAll modal "xpath/./div[@class='agreementModal']/div"

                p.Keyboard.PressAsync("Escape") |> wait // press Escape so we can get details for next invoice
                sleep 2
                yield invoice_row_cells, modal_rows ]

        invoices

    member this.SignIn() =
        if p = null then
            p <- getPage args
        if signedIn |> not then
            signInInternal()

    member this.SubmitIndication(indication) =
        goto p "https://ebok.pgnig.pl/odczyt"
        waitTillHTMLRendered p
        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        if isTest |> not then
            typet p "xpath///input[@id='reading-0']" (indication |> string)
            click p "xpath///button[contains(text(), 'Zapisz odczyt')]"
            click p "xpath///button[contains(text(), 'Tak')]"
            waitTillHTMLRendered p // make sure the input was accepted
        else
            printfn "Skipping indication submission in test mode"

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

        goto p "https://ebok.pgnig.pl/faktury"
        sleep 2
        ScrapeInvoicesInternal () |> List.map parseInvoiceToStrings

    member this.ScrapeOverpayments() =
        goto p "https://ebok.pgnig.pl/umowy"
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

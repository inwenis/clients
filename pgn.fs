namespace clients

open PuppeteerSharp
open Utils

module PGNIG =

    let loginPage_userNameSelector = """#main > div > div > div.remove-tablet.columns.large-4.medium-4.small-12.login-block > div > div.flip-container.row.login > div > form > div > div > div > label:nth-child(1) > input[type=text]"""
    let loginPage_passwordSelector = """#main > div > div > div.remove-tablet.columns.large-4.medium-4.small-12.login-block > div > div.flip-container.row.login > div > form > div > div > div > label:nth-child(2) > div.relative > input[type=password]"""

    type InvoiceData = {
        BeforeClickingOnInvoice: string list
        AfterClickingOnInvoice: string list
    }

    type PGNiGClient(username, password) =
        let mutable signedIn = false
        let mutable p : IPage = null
        do
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait

        member this.SingIn() =
            if signedIn |> not then
                p <-
                    let arg = [| "--disable-notifications"; "--force-device-scale-factor=0.5" |]

                    let opt =
                        new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions(), Args = arg)

                    let brw = Puppeteer.LaunchAsync opt |> runSync
                    brw.PagesAsync() |> runSync |> Array.exactlyOne

                let w = p.WaitForNetworkIdleAsync()
                p.GoToAsync "https://ebok.pgnig.pl/" |> wait
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                click p "xpath///button[text()='Odrzuć wszystkie']"
                sleep 1
                click p "xpath///i[contains(@class,'icon-close')]"
                sleep 1
                typet p loginPage_userNameSelector (username ())
                typet p loginPage_passwordSelector (password ())
                let w = p.WaitForNetworkIdleAsync()
                sleep 1 // I have experienced that without waiting here clicking the "submit" button has no effect
                click p "xpath///button[@type='submit']"
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                signedIn <- true

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

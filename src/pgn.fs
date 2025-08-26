namespace clients

open PuppeteerSharp
open Utils

module PGNIG =
    open BaseClient

    type InvoiceData = {
        BeforeClickingOnInvoice: string list
        AfterClickingOnInvoice: string list
    }

    type PGNiGClient(username, password, args, page, isSignedIn, isTest) =
        inherit BaseClient(username, password, args, page, isSignedIn, isTest)

        let mutable signedIn = false

        new(usr, pwd, args) = PGNiGClient(usr, pwd, args, null, false, false)

        member this.SignIn() =
            this.InitializePage()
            if signedIn |> not then
                let w = this.Page.WaitForNetworkIdleAsync()
                this.Page.GoToAsync "https://ebok.pgnig.pl/" |> wait
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                click this.Page "xpath///button[text()='Odrzuć wszystkie']"
                sleep 1
                click this.Page "xpath///i[contains(@class,'icon-close')]"
                sleep 1
                typet this.Page "xpath///input[@name='identificator']" (username ())
                typet this.Page "xpath///input[@name='accessPin']" (password ())
                let w = this.Page.WaitForNetworkIdleAsync()
                sleep 1 // I have experienced that without waiting here clicking the "submit" button has no effect
                click this.Page "xpath///button[@type='submit']"
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                signedIn <- true

        member this.SubmitIndication(indication) =
            this.Page.GoToAsync "https://ebok.pgnig.pl/odczyt" |> wait

            printfn "Waiting for page to load... "
            waitTillHTMLRendered this.Page
            printfn "done"

            click this.Page "xpath///i[contains(@class,'icon-close')]"
            sleep 1

            this.Page.TypeAsync("xpath///input[@id='reading-0']", indication |> string) |> wait

            click this.Page "xpath///button[contains(text(), 'Zapisz odczyt')]"

            click this.Page "xpath///button[contains(text(), 'Tak')]"
            // make sure the input was accepted
            printfn "Waiting for page to load... "
            waitTillHTMLRendered this.Page
            printfn "done"

        member this.ScrapeInvoicesInternal() =
            // we rely on the index here because the list is rebuild and DOM nodes are detached
            let invoice_indexes =
                this.Page.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result
                |> Array.mapi (fun i _ -> i)

            let invoices = [
                for index in invoice_indexes do
                    let invoice_row =
                        this.Page.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result
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
                        let details_text = this.Page.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result.GetPropertyAsync("textContent").Result |> string // get all the text of the modal that displays the invoice's details
                        if details_text.Contains("Numer faktury") then details_loaded <- true
                        if details_text.Contains("Numer noty") then details_loaded <- true

                    let modal = this.Page.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result
                    let modal_rows = modal.QuerySelectorAllAsync("xpath/./div[@class='agreementModal']/div").Result

                    this.Page.Keyboard.PressAsync("Escape").Wait() // press Escape so we can get details for next invoice
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

            this.Page.EvaluateExpressionAsync("() => document.body.style.zoom = 0.5").Wait()

            this.Page.GoToAsync "https://ebok.pgnig.pl/faktury" |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            sleep 2
            let invoices = this.ScrapeInvoicesInternal()

            invoices
            |> List.map parseInvoiceToStrings

        member this.ScrapeOverpayments() =
            this.Page.GoToAsync "https://ebok.pgnig.pl/umowy" |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            sleep 2

            let rows = this.Page.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result

            rows
            |> List.ofArray
            |> List.map (fun row ->
                row.QuerySelectorAllAsync("xpath/.//div[contains(@class,'columns')]").Result
                |> List.ofArray
                |> List.map (fun cell -> cell.EvaluateFunctionAsync<string>("el => el.textContent").Result)
                |> List.map (fun x -> x.Trim())
                |> List.filter (fun x -> x <> "") )

namespace clients

open PuppeteerSharp
open Utils
open System.Text.RegularExpressions

module PGNIG =

    let loginPage_userNameSelector = """#main > div > div > div.remove-tablet.columns.large-4.medium-4.small-12.login-block > div > div.flip-container.row.login > div > form > div > div > div > label:nth-child(1) > input[type=text]"""
    let loginPage_passwordSelector = """#main > div > div > div.remove-tablet.columns.large-4.medium-4.small-12.login-block > div > div.flip-container.row.login > div > form > div > div > div > label:nth-child(2) > div.relative > input[type=password]"""
    let loginPage_signInButton     = """#main > div > div > div.remove-tablet.columns.large-4.medium-4.small-12.login-block > div > div.flip-container.row.login > div > form > div > div > div > button"""

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
                    let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
                    let b = Puppeteer.LaunchAsync(l_options) |> runSync
                    b.PagesAsync() |> runSync |> Array.exactlyOne

                let w = p.WaitForNetworkIdleAsync()
                p.GoToAsync("https://ebok.pgnig.pl/") |> wait
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                typet p loginPage_userNameSelector (username())
                typet p loginPage_passwordSelector (password())
                let w = p.WaitForNetworkIdleAsync()
                click p loginPage_signInButton
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                signedIn <- true

        member this.SubmitIndication(indication) =
            p.GoToAsync "https://ebok.pgnig.pl/odczyt" |> wait

            printfn "Waiting for page to load... "
            waitTillHTMLRendered p
            printfn "done"

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
                        let details_text = p.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result.GetPropertyAsync("textContent").Result |> string // get all the text of the modal that displays the invoice's details
                        if details_text.Contains("Numer faktury") then details_loaded <- true
                        if details_text.Contains("Numer noty") then details_loaded <- true
                        sleep 1

                    let modal = p.QuerySelectorAsync("xpath///div[@class='ModalContent']").Result
                    let modal_rows = modal.QuerySelectorAllAsync("xpath/./div[@class='agreementModal']/div").Result

                    p.Keyboard.PressAsync("Escape").Wait() // press Escape so we can get details for next invoice
                    sleep 2
                    yield invoice_row_cells, modal_rows ]

            invoices

        member this.ProcessInvoices(invoices:list<array<IElementHandle> * array<IElementHandle>>) =
            let get_text (x:IElementHandle) = x.GetPropertyAsync("innerText").Result.JsonValueAsync().Result |> string

            invoices
            |> List.map (fun (a,b) ->
                // parse only relevant stuff so changes in irrelevant fields don't cause parsing to break

                // sample text - "16,33 zł0,00 zł"
                let invoice_amount =  a.[3] |> get_text |> regexExtractg "(.*?) zł.*? zł" |> regexReplace "," "." |> decimal

                // sample text - "Numer faktury: 826560/118/2024/FData wystawienia: 13-03-2024"
                let date = b.[0] |> get_text |> regexExtractg "Data wystawienia: (.+)" |> fun x -> System.DateTimeOffset.ParseExact(x, "dd-MM-yyyy", null)

                // sample text - "Umowa wygasła - brak danych"
                // sample text - "Adres:  80-433  ***REMOVED***, ul. Ludwika Waryńskiego 46 B /6"
                let address = b.[2] |> get_text |> regexExtractg "(Umowa wygasła|Adres:\s.+)"

                address, date, invoice_amount
            )
            |> List.filter (fun (a, d, i) -> a <> "Umowa wygasła")
            |> List.map (fun (a, d, i) ->
                let curve =
                    match a |> regexRemove "\s" with
                    | "Adres:***REMOVED***"
                    | "Adres:***REMOVED***"     -> "***REMOVED***"
                    | "Adres:***REMOVED***"
                    | "Adres:***REMOVED***" -> "***REMOVED***"
                curve, d, i
            )
            |> List.groupBy (fun (curve, _, _) -> curve)
            |> List.map (fun (c, data) -> c, data |> List.map (fun (_,x,y) -> x,y))


        member this.ScrapeInvoices() =
            p.GoToAsync("https://ebok.pgnig.pl/faktury") |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            sleep 2
            let invoices = this.ScrapeInvoicesInternal()
            this.ProcessInvoices(invoices)

        member this.ScrapeOverpayments() =
            p.GoToAsync("https://ebok.pgnig.pl/umowy") |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            sleep 2

            let rows = p.QuerySelectorAllAsync("xpath///div[contains(@class,'table-row')]").Result

            // JSHandle:010/2021/157244/UI54563288018590365500027859174 010/2021/157244/UI***REMOVED*** ***REMOVED*** ***REMOVED***19,65 zł
            // JSHandle:010/2019/5010/UI8265608018590365500021566160 010/2019/5010/UI***REMOVED*** ***REMOVED*** ***REMOVED***

            let parseRow text =
                let dic = new System.Collections.Generic.Dictionary<string,string>()
                // JSHandle:P/5456328/0005/23Faktura sprzedaż19-05-202337,63 zł0,00 zł80 kWhOpłacona
                let m = Regex.Match(text, "JSHandle:.*(?:***REMOVED***|***REMOVED***)(.*?)***REMOVED***(.*)")
                m.Groups.[1].Value |> fun x -> x.Trim() |> fun x -> dic.Add("adres", x)
                m.Groups.[2].Value |> fun x -> x.Trim() |> fun x -> dic.Add("amount", x)
                dic

            let nameToCurveMap x =
                match x with
                | "***REMOVED***"          -> "***REMOVED***"
                | "***REMOVED***" -> "***REMOVED***"

            let parseAmount x =
                match x with
                | "" -> 0M
                | _  -> x |> regexRemove " zł" |> regexReplace "," "." |> regexRemove "[a-z!]" |> decimal

            let data =
                rows
                |> List.ofArray
                |> List.map (fun x -> x.GetPropertyAsync("textContent").Result |> string)
                |> List.map parseRow
                |> List.map (fun x -> x.["adres"] |> nameToCurveMap, x.["amount"] |> parseAmount)

            data

        member this.GetP() = p

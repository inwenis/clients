namespace clients

open PuppeteerSharp
open Utils


module Energa =

    type EnergaClient() =
        let mutable signedIn = false
        let mutable p : IPage = null
        do
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait

    #if INTERACTIVE
        member this.GetP() = p
    #endif

        member this.SingIn() =
            if signedIn |> not then
                p <-
                    let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
                    let b = Puppeteer.LaunchAsync(l_options) |> runSync
                    b.PagesAsync() |> runSync |> Array.exactlyOne

                let w = p.WaitForNetworkIdleAsync()
                p.GoToAsync("https://www.24.energa.pl/") |> wait
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                typet p "xpath///input[@name='username']" "REMOVED"
                typet p "xpath///input[@name='password']" "REMOVED"
                let w = p.WaitForNetworkIdleAsync()
                click p "xpath///button[@name='login']"
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                signedIn <- true

        member this.SubmitIndication(accountName, indication) =
            p.GoToAsync "https://24.energa.pl/ss/select-invoice-profile" |> wait

            printfn "Waiting for page to load... "
            waitTillHTMLRendered p
            printfn "done"

            let w = p.WaitForNavigationAsync()
            click p $"xpath///label[contains(text(),'{accountName}')]"
            // we click a button that navigates us to a different address hence
            // we need to wait for the new page to load before we can continue
            printfn "Waiting for page to load... "
            w |> wait
            waitTillHTMLRendered p
            printfn "done"

            typet p "xpath///input[@name='value1']" $"{indication}"
            let w2 = p.WaitForNavigationAsync()
            click p "xpath///button[contains(text(),'Sprawdź')]"

            printfn "Waiting for page to load... "
            w2 |> wait
            waitTillHTMLRendered p
            printfn "done"

            printfn "dumping page in case extraction fails"
            let content = p.GetContentAsync().Result
            let tempFilePath = System.IO.Path.GetTempFileName()
            System.IO.File.WriteAllText(tempFilePath, content)
            printfn "dumped content to %A" tempFilePath

            printfn "extracting amount"
            let amountText =
                let node = p.WaitForSelectorAsync("xpath///*[contains(text(), 'Kwota do zapłaty')]").Result
                node.GetPropertyAsync("textContent").Result.ToString()
            let amount =
                amountText
                |> regexRemove "JSHandle:Kwota do zapłaty:"
                |> regexRemove "zł"
                |> regexReplace "," "."
                |> decimal
            printfn "Extracted amount %A" amount
            click p "xpath///button[contains(text(),'Zatwierdź')]"
            try
                printfn "Waiting for 'Gratulacje' to appear"
                p.WaitForSelectorAsync("xpath///*[contains(text(), 'Gratulacje')]") |> wait
                printfn "Clicking 'powrót'"
                click p "xpath///button[contains(text(),'powrót')]"
            with e -> printfn "%A" e
            amount

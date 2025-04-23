module Energa

open PuppeteerSharp
open Utils

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
                let b = Puppeteer.LaunchAsync(l_options) |> run_sync
                b.PagesAsync() |> run_sync |> Array.exactlyOne

            let w = p.WaitForNetworkIdleAsync()
            p.GoToAsync("https://www.24.energa.pl/") |> wait
            printf "Waiting for page to load... "
            w |> wait2
            printfn "done"
            waitForXpathAndType p "xpath///input[@name='username']" "REMOVED"
            waitForXpathAndType p "xpath///input[@name='password']" "REMOVED"
            let w = p.WaitForNetworkIdleAsync()
            waitForSelectorAndClick p "xpath///button[@name='login']"
            printf "Waiting for page to load... "
            w |> wait2
            printfn "done"
            signedIn <- true

    member this.SubmitIndication(accountName, indication) =
        p.GoToAsync "https://24.energa.pl/ss/select-invoice-profile" |> wait2

        printfn "Waiting for page to load... "
        waitTillHTMLRendered p
        printfn "done"

        // we will click a button that will navigate us to a different address so
        // we need to wait for it to happen before we can operate on the new site
        let w = p.WaitForNavigationAsync()
        waitAndClickXpathSyncAlsoWhenElementNotCurrentlyInView p $"xpath///label[contains(text(),'{accountName}')]"
        printfn "Waiting for page to load... "
        w |> wait2
        waitTillHTMLRendered p
        printfn "done"

        waitForXpathAndType p "xpath///input[@name='value1']" $"{indication}"
        let w2 = p.WaitForNavigationAsync()
        waitForSelectorAndClick p "xpath///button[contains(text(),'Sprawdź')]"

        printfn "Waiting for page to load... "
        w2 |> wait2
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
        waitForSelectorAndClick p "xpath///button[contains(text(),'Zatwierdź')]"
        try
            printfn "Waiting for 'Gratulacje' to appear"
            p.WaitForSelectorAsync("xpath///*[contains(text(), 'Gratulacje')]") |> wait2
            printfn "Clicking 'powrót'"
            waitForSelectorAndClick p "xpath///button[contains(text(),'powrót')]"
        with e -> printfn "%A" e
        amount

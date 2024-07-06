namespace clients

open System.Threading.Tasks
open PuppeteerSharp
open Utils
open Types

module Alior =
    open System.IO
    open System
    type AliorClient(username, password) =
        let mutable signedIn = false
        let mutable p : IPage = null
        do
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait

        member this.SignIn() =
            if signedIn |> not then
                p <-
                    let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
                    let b = Puppeteer.LaunchAsync(l_options) |> run_sync
                    b.PagesAsync() |> run_sync |> Array.exactlyOne
                p.GoToAsync("https://system.aliorbank.pl/sign-in", timeout=60 * 1000) |> wait
                waitForXpathAndType p "xpath///input[@id='login']" username
                waitForSelectorAndClick p "xpath///button[@title='Next']"
                waitForXpathAndType p "xpath///input[@id='password']" password
                waitForSelectorAndClick p "xpath///button[@id='password-submit']"
                waitForSelectorAndClick p "xpath///button[contains(text(),'One-time access')]"
                p.WaitForSelectorAsync("xpath///*[contains(text(),'My wallet')]") |> wait // we wait for the main page to load after logging in
                sleep 2
                signedIn <- true

        member this.OpenNewPayment() =
            this.SignIn()
            try
                // go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
                waitForSelectorAndClick p "xpath///*[contains(text(),'Dashboard')]"
                sleep 2
                waitForSelectorAndClick p "xpath///*[contains(text(),'Payments')]"
                sleep 1 // need to sleep otherwise the New Payment won't work
                waitForSelectorAndClick p "xpath///*[contains(text(),'New payment')]"
            with
            | e ->
                // on my laptop when the screen is too small the top menu is hidden and I need to first click 'Menu'
                waitForSelectorAndClick p "xpath///*[contains(text(),'Menu')]"
                sleep 1
                waitForSelectorAndClick p "xpath///*[contains(text(),'Dashboard')]"
                sleep 2 // somehow it didn't work without this wait
                waitForSelectorAndClick p "xpath///a/span[contains(text(),'Payments')]/.."
                sleep 1
                waitForSelectorAndClick p "xpath///*[contains(text(), 'New payment')]"

        member this.TransferRegular(transfer:Transfers.Row) =
            this.SignIn()
            this.OpenNewPayment()
            sleep 2 // if I don't wait before clicking the drop down it will not expand
            waitForSelectorAndClick p "xpath///accounts-select"
            let drop_down = p.WaitForSelectorAsync("xpath///accounts-select") |> run_sync
            transfer.FromAccount      |> fun x -> clickE $"xpath/(.//*[contains(text(), '{x}')])[last()]" drop_down
            transfer.ReceiverName     |> waitForXpathAndType p "xpath///*[@id='destination.name']"
            transfer.ReceiverAccount  |> waitForXpathAndType p "xpath///*[@id='account_number']"
            transfer.Amount |> string |> waitForXpathAndType p "xpath///*[@id='amount.value']"
            transfer.TransferText     |> waitForXpathAndType p "xpath///*[@id='title']"

            // sleep 1 since I can't use `wait for xpath` - at lest I need a better xpath for `wait for xpath` to work
            sleep 1
            p.QuerySelectorAllAsync("xpath///button")
            |> run_sync
            |> Array.filter (fun x -> x.QuerySelectorAllAsync("xpath/.//*[contains(text(), 'Next')]").Result.Length = 1 )
            |> Array.exactlyOne
            |> click

            let waitingForDomesticTransferFinish = p.WaitForSelectorAsync("xpath///*[contains(text(),'Domestic transfer submitted.')]") |> Async.AwaitTask |> Async.Ignore |> Async.StartAsTask :> Task
            let waitingForInternalTransferFinish = task {
                let! s = p.WaitForSelectorAsync("xpath///*[contains(text(),'Confirm')]")
                sleep 2
                do! s.ClickAsync()
                // after internal transfers it seems we're back at the "Create transfer page"
                do p.WaitForSelectorAsync("xpath///*[contains(text(),'Create domestic transfer')]") |> ignore
            }
            let x = waitingForInternalTransferFinish :> Task
            System.Threading.Tasks.Task.WaitAny([|waitingForDomesticTransferFinish; x|], 30 * 1000) |> ignore

            sleep 2

        member this.TransferTax(transfer:Transfers.Row, taxOfficeName) =
            this.SignIn()
            let year, month =
                let split = transfer.TransferText.Split("/")
                split.[0], split.[1]

            this.OpenNewPayment()
            sleep 2
            waitForSelectorAndClick p "xpath///*[contains(text(),'Tax transfer')]"
            sleep 2 // if I don't wait before clicking the drop down it will not expand
            waitForSelectorAndClick p "xpath///accounts-select"

            let drop_down = p.WaitForSelectorAsync("xpath///accounts-select") |> run_sync
            drop_down |> clickE $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]"

            waitForXpathAndType p "xpath///*[@id='form-symbol']" "PPE"
            waitForSelectorAndClick p "xpath///span[contains(text(),'PPE')]" // after typing the `tax form symbol` I have to select it from the drop-down

            waitForXpathAndType p $"xpath///*[@id='tax-department']" "{taxOfficeName}"
            waitForSelectorAndClick p $"xpath///span[contains(text(),'{taxOfficeName}')]" // after typing the `tax department` I have to select it from the drop-down

            waitForXpathAndType p "xpath///*[@id='department-account-number']" transfer.ReceiverAccount
            waitForXpathAndType p "xpath///*[@id='amount.value']" (transfer.Amount |> string)
            sleep 1

            // TODO - get proper xpath to select the drop-down
            p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']").Result.[0].ClickAsync().Wait()
            sleep 1
            let drop_down =
                p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']")
                |> run_sync
                |> Array.head
            drop_down |> clickE $"xpath/(.//*[contains(text(), 'Month')])[last()]"

            let e = p.WaitForSelectorAsync("xpath///*[@id='obligation_year']").Result
            // press backspace 4 times to remove year that is there by default
            for _ in [1..4] do
                e.PressAsync("Backspace").Wait()
            e.TypeAsync(year).Wait()

            p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']").Result.[1].ClickAsync().Wait()
            sleep 1
            let drop_down =
                p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']")
                |> run_sync
                |> Array.last
            drop_down |> clickE $"xpath/(.//*[contains(text(), '{month}')])[last()]"

            p.QuerySelectorAllAsync("xpath///button")
            |> run_sync
            |> Array.filter (fun x -> x.QuerySelectorAllAsync("xpath/.//*[contains(text(), 'Next')]").Result.Length = 1 )
            |> Array.exactlyOne
            |> click

            p.WaitForSelectorAsync("xpath///*[contains(text(),'Tax transfer sent')]") |> wait
            sleep 2

        member this.GetP() = p

        member this.Scrape() =
            this.SignIn()
            // click payments
            waitForSelectorAndClick p """/html/body/div/div/app-ajs-root/div/div[1]/internal/main-header/div/div[2]/div/section/div/div/div[2]/main-navigation/div/nav/ul/li[2]/a/span[1]"""
            sleep 2
            // click payment history
            waitForSelectorAndClick p """/html/body/div/div/app-ajs-root/div/div[1]/internal/main-header/div/div[2]/div/section/div/div/div[2]/main-navigation/div/nav/ul/li[2]/ul/li[2]/a/span"""
            sleep 2
            // Click show filters
            waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[1]/div/div/a"""
            sleep 2
            // click Period
            waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[3]/history-filter-time/fieldset/div/custom-select/div/div/div/div[1]"""
            sleep 2
            // click Last Year
            waitForSelectorAndClick p """xpath///*[@id="option_time_LAST_YEAR"]"""
            sleep 2

            // click file type
            waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[8]/history-export/div/div/div[2]/custom-select/div/div/div/div[1]"""
            sleep 2
            // click csv
            waitForSelectorAndClick p """//*[@id="option_document_type_CSV"]"""
            sleep 2

            let products =
                [
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                    """//*[@id="option_product_TODO"]"""
                ]

            // transactions must be downloaded per product separately. If all products are selected internal transaction are messed up.
            for product_selector in products do
                // click Product
                waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[6]/history-filter-product/fieldset/div/custom-select/div/span/div/div[1]"""
                sleep 2

                waitForSelectorAndClick p product_selector
                sleep 2
                p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
                sleep 2

                // Apply filters
                waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[9]/button-cta/button"""
                sleep 2

                // click Download
                waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[8]/history-export/div/div/div[3]/button-cta/button/span"""
                sleep 5
                printfn "File should be ready in \"Downloads\" folder"

                // deselect product
                // click Product
                waitForSelectorAndClick p """//*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[6]/history-filter-product/fieldset/div/custom-select/div/span/div/div[1]"""
                sleep 2
                waitForSelectorAndClick p product_selector
                sleep 2
                p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
                sleep 2

                let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                let file =
                    Directory.EnumerateFiles(Path.Combine(home,"Downloads"), "*.csv")
                    |> List.ofSeq
                    |> List.map              (fun x -> new FileInfo(x))
                    |> List.filter           (fun x -> DateTimeOffset.UtcNow - DateTimeOffset(x.CreationTimeUtc) < TimeSpan.FromSeconds(60.))
                    |> List.sortByDescending (fun x -> x.CreationTimeUtc)
                    |> List.tryHead

                match file with
                | Some f ->
                    printfn "found file, moving to 'finances'"
                    File.Copy(f.FullName, Path.Combine("./input_data/alior/", f.Name), true)
                | None -> printfn "not found"

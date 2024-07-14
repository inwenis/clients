namespace clients

open System.Threading.Tasks
open PuppeteerSharp
open Utils
open System.IO
open System
open FSharp.Data


type Transfers = CsvProvider<"""FromAccount,ReceiverName,ReceiverAccount,TransferText,Amount,InsertedDateTime,Status,Type
Billing account name is long to align with head,Receiver1,   12 1234 1234 12,Title of tra, 58.00,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align with head,Receiver3,   12 1234 1234 12,Title of tra, 12.52,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align with head,Receiver2,   12 1234 1234 12,Title of tra, 27.95,2023-03-01T00:00:00.0000000+02:00,Executed,Tax
""">

module Alior =

    type AliorClient private (username, password, p, signedIn, isTest) =
        let mutable signedIn = signedIn
        let mutable p : IPage = p
        do
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait

        new(username, password, p, isTest) =
            AliorClient(username, password, p, true, isTest)

        new(username, password, isTest) =
            AliorClient(username, password, null, false, isTest)

        member this.SignIn() =
            if signedIn |> not then
                p <-
                    let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
                    let b = Puppeteer.LaunchAsync(l_options) |> run_sync
                    b.PagesAsync() |> run_sync |> Array.exactlyOne
                p.GoToAsync("https://system.aliorbank.pl/sign-in", timeout=60 * 1000) |> wait
                waitForSelectorAndType p "xpath///input[@id='login']" (username ())
                waitForSelectorAndClick p "xpath///button[@title='Next']"
                waitForSelectorAndType p "xpath///input[@id='password']" (password ())
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
            transfer.FromAccount      |> fun x -> clickSelector $"xpath/(.//*[contains(text(), '{x}')])[last()]" drop_down
            transfer.ReceiverName     |> waitForSelectorAndType p "xpath///*[@id='destination.name']"
            transfer.ReceiverAccount  |> waitForSelectorAndType p "xpath///*[@id='account_number']"
            transfer.Amount |> string |> waitForSelectorAndType p "xpath///*[@id='amount.value']"
            transfer.TransferText     |> waitForSelectorAndType p "xpath///*[@id='title']"

            if isTest |> not then
                // sleep 1 since I can't use `wait for xpath` - at lest I need a better xpath for `wait for xpath` to work
                sleep 1
                p.QuerySelectorAllAsync("xpath///button")
                |> run_sync
                |> Array.filter (fun x -> x.QuerySelectorAllAsync("xpath/.//*[contains(text(), 'Next')]").Result.Length = 1 )
                |> Array.exactlyOne
                |> clickElement

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
            else
                printfn "Test mode - not sending the transfer"

        member this.TransferTax(transfer:Transfers.Row, taxOfficeName) =
            this.SignIn()
            let year, month =
                let split = transfer.TransferText.Split("/")
                split.[0], split.[1]

            this.OpenNewPayment()
            sleep 2
            waitForSelectorAndClick p "xpath///*[contains(text(),'Tax transfer')]"
            sleep 2

            let fromAccountDropDown = p.WaitForSelectorAsync("xpath///accounts-select") |> run_sync
            clickElement fromAccountDropDown
            fromAccountDropDown |> clickSelector $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]"

            waitForSelectorAndType p "xpath///*[@id='form-symbol']" "PPE"
            waitForSelectorAndClick p "xpath///span[contains(text(),'PPE')]" // after typing the `tax form symbol` I have to select it from the drop-down

            waitForSelectorAndType p $"xpath///*[@id='tax-department']" $"{taxOfficeName}"
            waitForSelectorAndClick p $"xpath///span[contains(text(),'{taxOfficeName}')]" // after typing the `tax department` I have to select it from the drop-down

            waitForSelectorAndType p "xpath///*[@id='department-account-number']" transfer.ReceiverAccount
            waitForSelectorAndType p "xpath///*[@id='amount.value']" (transfer.Amount |> string)
            sleep 1


            let periodDropDown = p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']").Result.[0]
            clickElement periodDropDown
            sleep 1
            clickSelector $"xpath/(.//*[contains(text(), 'Month')])[last()]" periodDropDown
            sleep 1
            // after selecting 'Month' the "Select month" drop-down appears
            let monthPeriodDropDown = p.QuerySelectorAsync("xpath/(//custom-select[@class='obligation-period-dropdown'])[last()]").Result

            let e = p.WaitForSelectorAsync("xpath///*[@id='obligation_year']").Result
            // press backspace 4 times to remove year that is there by default
            for _ in [1..4] do
                e.PressAsync("Backspace").Wait()
            e.TypeAsync(year).Wait()
            sleep 1

            clickElement monthPeriodDropDown
            monthPeriodDropDown |> clickSelector $"xpath/(.//*[contains(text(), '{month}')])[last()]"

            if isTest |> not then
                p.QuerySelectorAllAsync("xpath///button")
                |> run_sync
                |> Array.filter (fun x -> x.QuerySelectorAllAsync("xpath/.//*[contains(text(), 'Next')]").Result.Length = 1 )
                |> Array.exactlyOne
                |> clickElement

                // confirm/discard with phone here

                p.WaitForSelectorAsync("xpath///*[contains(text(),'Tax transfer sent')]") |> wait
                sleep 2
            else
                printfn "Test mode - not sending the transfer"

        member this.GetP() = p

        member this.Scrape() =
            let getAttributeNames = fun (d:IElementHandle) -> d.EvaluateFunctionAsync<string[]>("node => Array.from(node.attributes).map(x => x.name)").Result
            let getAttributeValue = fun name (d:IElementHandle) -> d.EvaluateFunctionAsync<string>($"node => node.getAttribute('{name}')").Result
            let getAttributes = fun (d:IElementHandle) ->
                let attributeNames = getAttributeNames d
                attributeNames
                |> List.ofArray
                |> List.map (fun x -> x, getAttributeValue x d)
                |> Map.ofList

            this.SignIn()
            // go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
            waitForSelectorAndClick p "xpath///*[contains(text(),'Dashboard')]"
            sleep 2
            waitForSelectorAndClick p "xpath///*[contains(text(),'Payments')]"
            sleep 2 // need to sleep otherwise the New Payment won't work
            waitForSelectorAndClick p "xpath///*[contains(text(),'Payment history')]"
            sleep 2
            waitForSelectorAndClick p "xpath///*[contains(text(),'Show filters')]"
            sleep 2
            // click Period
            waitForSelectorAndClick p "xpath///div[@id='list_time']/parent::div/parent::div"
            // todo - make this a moving range, picking just last year - see if we lose transaction on year change here
            waitForSelectorAndClick p """xpath///*[@id="option_time_LAST_YEAR"]"""
            // click File type
            waitForSelectorAndClick p "xpath///div[@id='list_document_type']/parent::div/parent::div"
            sleep 2
            // click csv
            waitForSelectorAndClick p """xpath///*[@id="option_document_type_CSV"]"""
            sleep 2

            // click Product
            waitForSelectorAndClick p "xpath///div[@id='list_product']/parent::div/parent::div"
            sleep 1
            let products = p.QuerySelectorAllAsync("xpath///*[contains(@id,'option_product')]") |> Async.AwaitTask |> Async.RunSynchronously

            // products must be accessed by xpaths because the DOM nodes are recreated with every opening of the "Product" drop-down
            let productsXpaths =
                products
                |> List.ofArray
                |> List.map getAttributes
                |> List.map (fun x -> x.["id"])
                |> List.map (fun x -> $"""xpath///*[@id="{x}"]""")

            // click Product to close dropdown
            waitForSelectorAndClick p "xpath///div[@id='list_product']/parent::div/parent::div"

            let mutable files = []
            // transactions must be downloaded per product separately. If all products are selected internal transaction are messed up.
            for product in productsXpaths do
                // click Product
                waitForSelectorAndClick p "xpath///div[@id='list_product']/parent::div/parent::div"
                sleep 2

                waitForSelectorAndClick p product
                sleep 2
                p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
                sleep 2

                // Apply filters
                waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[9]/button-cta/button"""
                sleep 2

                // click Download
                waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[8]/history-export/div/div/div[3]/button-cta/button/span"""
                sleep 5
                printfn "File should be ready in \"Downloads\" folder"

                // deselect product
                // click Product
                waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[6]/history-filter-product/fieldset/div/custom-select/div/span/div/div[1]"""
                sleep 2
                waitForSelectorAndClick p product
                sleep 2
                p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
                sleep 2

            let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            Directory.EnumerateFiles(Path.Combine(home,"Downloads"), "*.csv")
            |> List.ofSeq
            |> List.map              (fun x -> new FileInfo(x))
            |> List.filter           (fun x -> DateTimeOffset.UtcNow - DateTimeOffset(x.CreationTimeUtc) < TimeSpan.FromMinutes(5))
            |> List.sortByDescending (fun x -> x.CreationTimeUtc)

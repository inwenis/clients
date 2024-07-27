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

    type ScrapePeriod =
    | LastYear
    | All
    | OtherRange of DateOnly * DateOnly

    type AliorClient private (usernameFun, passwordFun, page, signedIn, isTest) =
        let mutable signedIn = signedIn
        let mutable p : IPage = page
        do
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait

        new(username, password, ?page, ?isTest) =
            let page, isAlreadySingedIn =
                match page with
                | Some x -> x, true
                | None -> null, false
            let isTest = isTest |> Option.defaultValue false
            AliorClient(username, password, page, isAlreadySingedIn, isTest)

        member this.SignIn() =
            if signedIn |> not then
                p <-
                    let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
                    let b = Puppeteer.LaunchAsync(l_options) |> runSync
                    b.PagesAsync() |> runSync |> Array.exactlyOne
                p.GoToAsync("https://system.aliorbank.pl/sign-in", timeout=60 * 1000) |> wait
                typet p "xpath///input[@id='login']" (usernameFun ())
                click p "xpath///button[@title='Next']"
                typet p "xpath///input[@id='password']" (passwordFun ())
                click p "xpath///button[@id='password-submit']"
                click p "xpath///button[contains(text(),'One-time access')]"
                p.WaitForSelectorAsync("xpath///*[contains(text(),'My wallet')]") |> wait // we wait for the main page to load after logging in
                sleep 2
                signedIn <- true

        member this.OpenNewPayment() =
            this.SignIn()
            // scrolling up by any amount (-1px in this case) makes the top menu appear (if it's hidden)
            p.EvaluateExpressionAsync("window.scrollBy(0, -1)") |> wait
            sleep 2
            try
                // go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
                click p "xpath///*[contains(text(),'Dashboard')]"
                sleep 2
                click p "xpath///*[contains(text(),'Payments')]"
                sleep 1 // need to sleep otherwise the New Payment won't work
                click p "xpath///*[contains(text(),'New payment')]"
            with
            | e ->
                // on my laptop when the screen is too small the top menu is hidden and I need to first click 'Menu'
                click p "xpath///*[contains(text(),'Menu')]"
                sleep 1
                click p "xpath///*[contains(text(),'Dashboard')]"
                sleep 2 // somehow it didn't work without this wait
                click p "xpath///a/span[contains(text(),'Payments')]/.."
                sleep 1
                click p "xpath///*[contains(text(), 'New payment')]"

        member this.TransferRegular(transfer:Transfers.Row) =
            this.SignIn()
            this.OpenNewPayment()
            sleep 2 // if I don't wait before clicking the drop down it will not expand
            click p "xpath///accounts-select"
            let accountsDropdown = p.WaitForSelectorAsync("xpath///accounts-select") |> runSync
            clickSelector $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]" accountsDropdown
            transfer.ReceiverName     |> typet p "xpath///*[@id='destination.name']"
            transfer.ReceiverAccount  |> typet p "xpath///*[@id='account_number']"
            transfer.Amount |> string |> typet p "xpath///*[@id='amount.value']"
            transfer.TransferText     |> typet p "xpath///*[@id='title']"

            if isTest |> not then
                sleep 2 // we need to wait otherwise we can't click 'Next'
                click p "xpath///button/*[contains(text(),'Next')]"
                // domestic transfers can be confirmed/discarded with phone
                // internal transfers are confirmed with a button automatically
                let domesticTranFin = async {
                    do! p.WaitForSelectorAsync("xpath///*[contains(text(),'Domestic transfer submitted.')]") |> Async.AwaitTask |> Async.Ignore
                    return Some() }

                let internalTranFin = async {
                    click p "xpath///*[contains(text(),'Confirm')]"
                    // after internal transfers it seems we're back at the "Create transfer page"
                    do! p.WaitForSelectorAsync("xpath///*[contains(text(),'Create domestic transfer')]") |> Async.AwaitTask |> Async.Ignore
                    return Some() }
                Async.Choice [internalTranFin; domesticTranFin] |> Async.RunSynchronously |> ignore
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
            click p "xpath///*[contains(text(),'Tax transfer')]"
            sleep 2

            let fromAccountDropDown = p.WaitForSelectorAsync("xpath///accounts-select") |> runSync
            clickElement fromAccountDropDown
            fromAccountDropDown |> clickSelector $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]"

            typet p "xpath///*[@id='form-symbol']" "PPE"
            click p "xpath///span[contains(text(),'PPE')]" // after typing the `tax form symbol` I have to select it from the drop-down

            typet p $"xpath///*[@id='tax-department']" $"{taxOfficeName}"
            click p $"xpath///span[contains(text(),'{taxOfficeName}')]" // after typing the `tax department` I have to select it from the drop-down

            typet p "xpath///*[@id='department-account-number']" transfer.ReceiverAccount
            typet p "xpath///*[@id='amount.value']" (transfer.Amount |> string)
            sleep 1


            let periodDropDown = p.QuerySelectorAllAsync("xpath///custom-select[@class='obligation-period-dropdown']") |> runSync |> Array.head
            clickElement periodDropDown
            sleep 1
            clickSelector $"xpath/(.//*[contains(text(), 'Month')])[last()]" periodDropDown
            sleep 1
            // after selecting 'Month' the "Select month" drop-down appears
            let monthPeriodDropDown = p.QuerySelectorAsync("xpath/(//custom-select[@class='obligation-period-dropdown'])[last()]") |> runSync

            let e = p.WaitForSelectorAsync("xpath///*[@id='obligation_year']") |> runSync
            // press backspace 4 times to remove year that is there by default
            for _ in [1..4] do
                e.PressAsync("Backspace") |> wait
            e.TypeAsync(year) |> wait
            sleep 1

            clickElement monthPeriodDropDown
            clickSelector $"xpath/(.//*[contains(text(), '{month}')])[last()]" monthPeriodDropDown

            if isTest |> not then
                sleep 2 // we need to wait otherwise we can't click 'Next'
                click p "xpath///button/*[contains(text(),'Next')]"
                // confirm/discard with phone here
                p.WaitForSelectorAsync("xpath///*[contains(text(),'Tax transfer sent')]") |> wait
                sleep 2
            else
                printfn "Test mode - not sending the transfer"

        member this.GetP() = p

        member this.Scrape(?period, ?count) =
            let period =
                match period with
                // we can't download transactions using the option 'All' but we can use 'Other range' with a wide range
                | Some All -> OtherRange(DateOnly.Parse("01.01.1990"), DateTime.Today |> DateOnly.FromDateTime)
                | Some x -> x
                | None -> LastYear
            let count =
                match count with
                | Some x -> x
                | None -> 100 // by default set count to 100 meaning we scrape all products

            this.SignIn()
            // go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
            click p "xpath///*[contains(text(),'Dashboard')]"
            sleep 2
            click p "xpath///*[contains(text(),'Payments')]"
            sleep 2 // need to sleep otherwise the New Payment won't work
            click p "xpath///*[contains(text(),'Payment history')]"
            sleep 2
            click p "xpath///*[contains(text(),'Show filters')]"
            sleep 2
            // click Period
            click p "xpath///div[@id='list_time']/parent::div/parent::div"

            match period with
            | LastYear -> click p """xpath///*[@id="option_time_LAST_YEAR"]"""
            | OtherRange (from, _to) ->
                click p """xpath///*[@id="option_time_OTHER_RANGE"]"""
                typeSlow p "xpath///input[@id='date-from']" "0" // need to type something first, otherwise the actual date won't be typed
                typeSlow p "xpath///input[@id='date-from']" (from.ToString("ddMMyyyy"))
                typeSlow p "xpath///input[@id='date-to']" "0" // need to type something first, otherwise the actual date won't be typed
                typeSlow p "xpath///input[@id='date-to']" (_to.ToString("ddMMyyyy"))
            | _ ->
                failwith "Not implemented"

            // click File type
            click p "xpath///div[@id='list_document_type']/parent::div/parent::div"
            sleep 2
            // click csv
            click p """xpath///*[@id="option_document_type_CSV"]"""
            sleep 2

            let productDropDown = "xpath///div[@id='list_product']/parent::div/parent::div"
            click p productDropDown
            sleep 2
            let products = p.QuerySelectorAllAsync("xpath///*[contains(@id,'option_product')]") |> runSync

            // products must be accessed by xpaths because the DOM nodes are recreated with every opening of the "Product" drop-down
            let productsXpaths =
                products
                |> List.ofArray
                |> List.map getAttributes
                |> List.map (fun x -> x.["id"])
                |> List.map (fun x -> $"""xpath///*[@id="{x}"]""")

            click p productDropDown // close drop-down
            sleep 2

            // transactions must be downloaded per product separately. If all products are selected internal transaction are messed up.
            for product in productsXpaths |> List.truncate count do
                click p productDropDown
                sleep 2
                click p product
                sleep 2
                click p productDropDown // close drop-down
                sleep 2

                click p "xpath///*[contains(text(),'Apply filters')]"
                sleep 2

                click p "xpath///*[contains(text(),'Download')]"
                sleep 5
                printfn "File should be ready in \"Downloads\" folder"

                click p productDropDown
                sleep 2
                click p product  // deselect current product
                sleep 2
                click p productDropDown // close drop-down
                sleep 2

            let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            Directory.EnumerateFiles(Path.Combine(home,"Downloads"), "*.csv")
            |> List.ofSeq
            |> List.map              (fun x -> new FileInfo(x))
            |> List.filter           (fun x -> DateTimeOffset.UtcNow - DateTimeOffset(x.CreationTimeUtc) < TimeSpan.FromMinutes(5))
            |> List.sortByDescending (fun x -> x.CreationTimeUtc)
            |> List.map              (fun x -> x.FullName)

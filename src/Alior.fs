module Alior

open PuppeteerSharp
open Utils
open System.IO
open System
open System.Text
open FSharp.Data


type Transfers =
    CsvProvider<"""FromAccount,ReceiverName,ReceiverAccount,TransferText,Amount,InsertedDateTime,Status,Type
Billing account name is long to align      ,Receiver1   ,12 1234 1234 12,Title of tra, 58.00,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align      ,Receiver3   ,12 1234 1234 12,Title of tra, 12.52,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align      ,Receiver2   ,12 1234 1234 12,Title of tra, 27.95,2023-03-01T00:00:00.0000000+02:00,Executed,Tax
""">


let ALIOR_ENCODING = CodePagesEncodingProvider.Instance.GetEncoding 1250

type ScrapePeriod =
    | LastYear
    | All
    | OtherRange of DateOnly * DateOnly

let HOME = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
let DOWNLOADS = Path.Combine(HOME, "Downloads") // puppeteer downloads files to this folder
let DEFAULT_DESTINATION = DOWNLOADS

/// <summary>Creates Alior client</summary>
/// <param name="username"></param>
/// <param name="password"></param>
/// <param name="args"></param>
/// <param name="page"></param>
/// <param name="isSignedIn"></param>
/// <param name="isTest">defaults to 'true' to avoid accidentally sending executing transfers</param>
type AliorClient(username, password, ?args, ?page: IPage, ?isSignedIn, ?isTest) =
    let isTest = isTest |> Option.defaultValue true

    let p, isSignedIn =
        match page, isSignedIn with
        | Some p, Some s   -> p, s
        | Some p, None     -> p, true
        | None, Some false -> null, false
        | None, Some true  -> failwith "You can not be signed in if you don't give me a page"
        | None, None       -> null, false

    let args =
        match args with
        | Some a -> a
        | None ->
            [| "--lang=en-GB" // many xpaths depend on English texts, this arg ensures the browser is launched in English
               // the default window run on my laptop is too small to display the whole menu
               // instead of supporting "hidden" menu we just "zoom out" by setting this arg
               "--force-device-scale-factor=0.5" |]

    let mutable signedIn = isSignedIn
    let mutable p: IPage = p

    do downloadDefaultBrowser ()

    let signInInternal () =
        gotoWithCustomTimeOut p "https://system.aliorbank.pl/sign-in" (60 * 1000)
        typet p "xpath///input[@id='login']" (username ())
        click p "xpath///button[@title='Next']"
        typet p "xpath///input[@id='password']" (password ())
        click p "xpath///button[@id='password-submit']"
        click p "xpath///button[contains(text(),'One-time access')]"
        waitSelector p "xpath///*[contains(text(),'My wallet')]" |> ignore // we wait for the main page to load after logging in
        sleep 2
        signedIn <- true

    member this.SignIn() =
        try
            if p = null then
                p <- getPage args
            if signedIn |> not then
                signInInternal ()
        with e ->
            dumpSnapshot p
            raise e

    member private this.OpenNewPayment() =
        this.SignIn()
        // scrolling up by any amount (-1px in this case) makes the top menu appear (if it's hidden)
        p.EvaluateExpressionAsync "window.scrollBy(0, -1)" |> wait
        sleep 2

        try
            // go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
            click p "xpath///*[contains(text(),'Dashboard')]"
            sleep 2
            click p "xpath///*[contains(text(),'Payments')]"
            sleep 1 // need to sleep otherwise the New Payment won't work
            click p "xpath///*[contains(text(),'New payment')]"
        with e ->
            // on my laptop when the screen is too small the top menu is hidden and I need to first click 'Menu'
            click p "xpath///*[contains(text(),'Menu')]"
            sleep 1
            click p "xpath///*[contains(text(),'Dashboard')]"
            sleep 2 // somehow it didn't work without this wait
            click p "xpath///a/span[contains(text(),'Payments')]/.."
            sleep 1
            click p "xpath///*[contains(text(), 'New payment')]"

    member private this.TransferRegularInternal(transfer: Transfers.Row) =
        this.SignIn()
        this.OpenNewPayment()
        sleep 2 // if I don't wait before clicking the drop down does not expand
        let accountsDropdown = queryFirst p "xpath///accounts-select"
        clickElement accountsDropdown
        sleep 1 // we need to wait before selecting the account otherwise we get a "node detached" error
        clickSelector $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]" accountsDropdown
        typet p "xpath///*[@id='destination.name']"    transfer.ReceiverName
        typet p "xpath///*[@id='account_number']"      transfer.ReceiverAccount
        typet p "xpath///*[@id='amount.value']"       (transfer.Amount |> string)
        typet p "xpath///*[@id='title']"               transfer.TransferText

        if isTest |> not then
            sleep 2 // we need to wait otherwise we can't click 'Next'
            click p "xpath///button/*[contains(text(),'Next')]"
            sleep 2 // wait for the next page to load

            // internal transfers are confirmed with a button automatically
            // external transfers are confirmed with a phone by the user manually
            let confirmInternalTransferButton =
                queryFirst p "xpath///*[contains(text(),'Confirm')]"

            if confirmInternalTransferButton <> null then
                clickElement confirmInternalTransferButton

            waitSelector p "xpath///*[contains(text(),'Domestic transfer submitted.')]"
            |> ignore

            sleep 2
        else
            printfn "Test mode - not sending the transfer"

    member this.TransferRegular(transfer: Transfers.Row) =
        try
            this.TransferRegularInternal(transfer)
        with e ->
            dumpSnapshot p
            raise e

    member private this.TransferTaxInternal(transfer: Transfers.Row, taxOfficeName) =
        this.SignIn()

        let year, month =
            let split = transfer.TransferText.Split "/"
            split.[0], split.[1]

        this.OpenNewPayment()
        sleep 2
        click p "xpath///*[contains(text(),'Tax transfer')]"
        sleep 2

        let fromAccountDropDown = waitSelector p "xpath///accounts-select"
        clickElement fromAccountDropDown
        clickSelector $"xpath/(.//*[contains(text(), '{transfer.FromAccount}')])[last()]" fromAccountDropDown

        typet p "xpath///*[@id='form-symbol']" "PPE"
        sleep 2 // wait for the drop-down to appear, otherwise we will click it too early and it won't work
        click p "xpath///span[contains(text(),'PPE')]" // after typing the `tax form symbol` I have to select it from the drop-down

        typet p $"xpath///*[@id='tax-department']" $"{taxOfficeName}"
        sleep 3 // wait for the drop-down to appear, otherwise we will click it too early and it won't work
        click p $"xpath///span[contains(text(),'{taxOfficeName}')]" // after typing the `tax department` I have to select it from the drop-down

        typet p "xpath///*[@id='department-account-number']" transfer.ReceiverAccount
        typet p "xpath///*[@id='amount.value']" (transfer.Amount |> string)
        sleep 1

        let periodDropDown =
            queryFirst p "xpath///custom-select[@class='obligation-period-dropdown']"

        clickElement periodDropDown
        sleep 1
        clickSelector $"xpath/(.//*[contains(text(), 'Month')])[last()]" periodDropDown
        sleep 1
        // after selecting obligation-period='Month' the "Month" and "Year" input fields appear

        let e = waitSelector p "xpath///*[@id='obligation_year']"
        // press backspace 4 times to remove year that is there by default
        for _ in [ 1..4 ] do
            e.PressAsync "Backspace" |> wait

        e.TypeAsync(year) |> wait
        sleep 1

        let monthPeriodDropDown =
            queryFirst p "xpath/(//custom-select[@class='obligation-period-dropdown'])[last()]"

        clickElement monthPeriodDropDown
        sleep 1
        clickSelector $"xpath/(.//*[contains(text(), '{month}')])[last()]" monthPeriodDropDown

        if isTest |> not then
            sleep 2 // we need to wait otherwise we can't click 'Next'
            click p "xpath///button/*[contains(text(),'Next')]"
            // confirm/discard with phone here
            waitSelector p "xpath///*[contains(text(),'Tax transfer sent')]" |> ignore
            sleep 2
        else
            printfn "Test mode - not sending the transfer"

    member this.TransferTax(transfer: Transfers.Row, taxOfficeName) =
        try
            this.TransferTaxInternal(transfer, taxOfficeName)
        with e ->
            dumpSnapshot p
            raise e

    member private this.ScrapeInternal(?destination, ?period, ?count) =
        let dest = destination |> Option.defaultValue DEFAULT_DESTINATION

        let period =
            match period with
            // we can't download transactions using the option 'All' but we can use 'Other range' with a wide range
            | Some All -> OtherRange(DateOnly.Parse "01.01.1990", DateTime.Today |> DateOnly.FromDateTime)
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
        | OtherRange(from, _to) ->
            click p """xpath///*[@id="option_time_OTHER_RANGE"]"""
            // We need to type something first, otherwise the actual date won't be typed hence we type "0".
            // When setting dates with `document.querySelector("input[#date-from").value = '...' the form claims dates are invalid.
            // The format of dates depends on Windows's ShortDate format - hence we use .ToShortDateString() and remove all non-digit characters.
            typeSlow p "xpath///input[@id='date-from']" "0"
            typeSlow p "xpath///input[@id='date-from']" (from.ToShortDateString() |> regexRemove "\D")
            typeSlow p "xpath///input[@id='date-to']" "0"
            typeSlow p "xpath///input[@id='date-to']" (_to.ToShortDateString() |> regexRemove "\D")
        | _ -> failwith "Not implemented"

        // click File type
        click p "xpath///div[@id='list_document_type']/parent::div/parent::div"
        sleep 2
        // click csv
        click p """xpath///*[@id="option_document_type_CSV"]"""
        sleep 2

        let productDropDown = "xpath///div[@id='list_product']/parent::div/parent::div"
        click p productDropDown
        sleep 2

        // products must be accessed by xpaths because the DOM nodes are recreated with every opening of the "Product" drop-down
        let productsXpaths =
            queryAll p "xpath///*[contains(@id,'option_product')]"
            |> Array.map getAttributes
            |> Array.map (fun x -> x.["id"])
            |> Array.map (fun x -> $"""xpath///*[@id="{x}"]""")

        // transactions must be downloaded per product separately. If all products are selected internal transaction are messed up.
        use w = new FileSystemWatcher(DOWNLOADS, "*.csv")
        let downloadedFiles = [
            for product in productsXpaths |> Array.truncate count do
                click p product
                sleep 2
                click p productDropDown // close drop-down
                sleep 2

                click p "xpath///*[contains(text(),'Apply filters')]"
                sleep 2

                click p "xpath///*[contains(text(),'Download')]"
                // watch for WatcherChangeTypes.All because the file is initially created as a temporary file
                // and when the downloading is finished it is renamed to its final name
                // this rename triggers the watcher because the new name ends with a .csv
                let s = w.WaitForChanged(WatcherChangeTypes.All, TimeSpan.FromSeconds 10.0)
                if s.TimedOut then
                    printfn "Downloading the file timed out, continuing with the next product"
                else
                    printfn "File downloaded: %s" s.Name
                    yield s.Name

                click p productDropDown
                sleep 2
                click p product // deselect current product
                sleep 2 ]

        let sourceFiles =
            downloadedFiles
            |> List.map (fun x -> new FileInfo(Path.Combine(DOWNLOADS, x)))

        let destinationFiles =
            sourceFiles
            |> List.map (fun x -> Path.Combine(dest, x.Name))

        for src, dest in List.zip sourceFiles destinationFiles do
            // save the files encoded as UTF-8 because I can't stand working with files encoded as Windows-1250
            let text = File.ReadAllText(src.FullName, ALIOR_ENCODING)
            File.WriteAllText(dest, text, Encoding.UTF8)

        destinationFiles

    member this.Scrape(?destination, ?period, ?count) =
        try
            this.ScrapeInternal(?destination=destination, ?period=period, ?count=count)
        with e ->
            dumpSnapshot p
            raise e

    member this.GetP() = p

module Energa

open System
open PuppeteerSharp
open Utils
open System.IO
open System.IO
open System.Text
open System.Threading.Tasks
open PuppeteerSharp
open System.Text.Json.Nodes
open System.Text.Json

type EnergaClient(username, password, ?args, ?page : IPage, ?isSignedIn, ?isTest) =
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

    let dumpMhtml (page: IPage) = task {
        let! client = page.Target.CreateCDPSessionAsync()
        let! raw = client.SendAsync("Page.captureSnapshot", {| format = "mhtml" |})
        // Avoid referencing Newtonsoft types by treating the result as obj → JSON string
        let json = raw.ToString()
        use doc = JsonDocument.Parse(json)
        let mhtml = doc.RootElement.GetProperty("data").GetString()
        let n = DateTime.Now.ToString("O").Replace(":", "_")
        do! File.WriteAllTextAsync($"page_{n}.mhtml", mhtml, Encoding.UTF8)
    }

    let signInInternal () =
        let w = p.WaitForNetworkIdleAsync()
        goto p "https://www.24.energa.pl/"
        w |> wait
        dumpMhtml p |> wait
        typet p "xpath///input[@name='username']" (username())
        typet p "xpath///input[@name='password']" (password())
        let w = p.WaitForNetworkIdleAsync()
        click p "xpath///button[@name='login']"
        w |> wait
        dumpMhtml p |> wait
        signedIn <- true

    member this.SignIn() =
        if p = null then
            p <- getPage args
        if signedIn |> not then
            signInInternal()

    member this.SubmitIndication(accountName, indication) =
        goto p "https://24.energa.pl/ss/select-invoice-profile"
        waitTillHTMLRendered p
        dumpMhtml p |> wait

        let w = p.WaitForNavigationAsync()
        click p $"xpath///label[contains(text(),'{accountName}')]"
        // we click a button that navigates us to a different address hence
        // we need to wait for the new page to load before we can continue
        w |> wait
        waitTillHTMLRendered p
        dumpMhtml p |> wait

        if isTest |> not then
            typet p "xpath///input[@name='value1']" $"{indication}"
            let w = p.WaitForNavigationAsync()
            click p "xpath///button[contains(text(),'Sprawdź')]"
            w |> wait

            waitTillHTMLRendered p

            printfn "dumping page in case extraction fails"
            let filePath = dumpPage p
            printfn "dumped content to %A" filePath

            printfn "extracting amount"
            let amountText = queryFirst p "xpath///*[contains(text(), 'Kwota do zapłaty')]" |> getText
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
                waitSelector p "xpath///*[contains(text(), 'Gratulacje')]" |> ignore
                printfn "Clicking 'powrót'"
                click p "xpath///button[contains(text(),'powrót')]"
            with e -> printfn "%A" e
            amount
        else
            printfn "Skipping indication submission in test mode"
            dumpMhtml p |> wait
            Decimal.MinValue // return a value indicating no submission occurred

    member this.GetP() = p

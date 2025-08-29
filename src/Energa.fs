module Energa

open PuppeteerSharp
open Utils


type EnergaClient(username, password, ?args, ?page : IPage, ?isSignedIn, ?isTest) =
    let isTest = isTest |> Option.defaultValue true
    let p, isSignedIn =
        match page, isSignedIn with
        | Some p, Some s     -> p, s
        | Some p, None       -> p, true
        | None,   Some false -> null, false
        | None,   Some true  -> failwith "You can not be signed in if you don't give me a page"
        | None,   None       -> null, false

    let mutable signedIn = isSignedIn
    let mutable p : IPage = p

    do downloadDefaultBrowser ()

    let signInInternal () =
        let w = p.WaitForNetworkIdleAsync()
        p.GoToAsync("https://www.24.energa.pl/") |> wait
        printf "Waiting for page to load... "
        w |> wait
        printfn "done"
        username() |> typet p "xpath///input[@name='username']"
        password() |> typet p "xpath///input[@name='password']"
        let w = p.WaitForNetworkIdleAsync()
        click p "xpath///button[@name='login']"
        printf "Waiting for page to load... "
        w |> wait
        printfn "done"
        signedIn <- true

    member this.SignIn() =
        if p = null then
            p <- getPage args
        if signedIn |> not then
            signInInternal()

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

    member this.GetP() = p

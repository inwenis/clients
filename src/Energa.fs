namespace clients

open PuppeteerSharp
open Utils


module Energa =
    open BaseClient

    type EnergaClient(username, password, args, page, isSignedIn, isTest) =
        inherit BaseClient(username, password, args, page, isSignedIn, isTest)

        let mutable signedIn = false

        member this.SignIn() =
            this.InitializePage()
            if signedIn |> not then

                let w = this.Page.WaitForNetworkIdleAsync()
                this.Page.GoToAsync("https://www.24.energa.pl/") |> wait
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                username() |> typet this.Page "xpath///input[@name='username']"
                password() |> typet this.Page "xpath///input[@name='password']"
                let w = this.Page.WaitForNetworkIdleAsync()
                click this.Page "xpath///button[@name='login']"
                printf "Waiting for page to load... "
                w |> wait
                printfn "done"
                signedIn <- true

        member this.SubmitIndication(accountName, indication) =
            this.Page.GoToAsync "https://24.energa.pl/ss/select-invoice-profile" |> wait

            printfn "Waiting for page to load... "
            waitTillHTMLRendered this.Page
            printfn "done"

            let w = this.Page.WaitForNavigationAsync()
            click this.Page $"xpath///label[contains(text(),'{accountName}')]"
            // we click a button that navigates us to a different address hence
            // we need to wait for the new page to load before we can continue
            printfn "Waiting for page to load... "
            w |> wait
            waitTillHTMLRendered this.Page
            printfn "done"

            typet this.Page "xpath///input[@name='value1']" $"{indication}"
            let w2 = this.Page.WaitForNavigationAsync()
            click this.Page "xpath///button[contains(text(),'Sprawdź')]"

            printfn "Waiting for page to load... "
            w2 |> wait
            waitTillHTMLRendered this.Page
            printfn "done"

            printfn "dumping page in case extraction fails"
            let content = this.Page.GetContentAsync().Result
            let tempFilePath = System.IO.Path.GetTempFileName()
            System.IO.File.WriteAllText(tempFilePath, content)
            printfn "dumped content to %A" tempFilePath

            printfn "extracting amount"
            let amountText =
                let node = this.Page.WaitForSelectorAsync("xpath///*[contains(text(), 'Kwota do zapłaty')]").Result
                node.GetPropertyAsync("textContent").Result.ToString()
            let amount =
                amountText
                |> regexRemove "JSHandle:Kwota do zapłaty:"
                |> regexRemove "zł"
                |> regexReplace "," "."
                |> decimal
            printfn "Extracted amount %A" amount
            click this.Page "xpath///button[contains(text(),'Zatwierdź')]"
            try
                printfn "Waiting for 'Gratulacje' to appear"
                this.Page.WaitForSelectorAsync("xpath///*[contains(text(), 'Gratulacje')]") |> wait
                printfn "Clicking 'powrót'"
                click this.Page "xpath///button[contains(text(),'powrót')]"
            with e -> printfn "%A" e
            amount

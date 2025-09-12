module PGNIG

open PuppeteerSharp
open Utils


type InvoiceData = {
    BeforeClickingOnInvoice: string []
    AfterClickingOnInvoice: string []
}

type PGNiGClient(username, password, ?args, ?page: IPage, ?isSignedIn, ?isTest) =
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
    let mutable p: IPage = p

    do downloadDefaultBrowser ()

    let signInInternal () =
        let w = p.WaitForNetworkIdleAsync()
        goto p "https://ebok.pgnig.pl/"
        w |> wait
        dumpSnapshot p
        click p "xpath///button[text()='Odrzuć wszystkie']"
        sleep 1
        dumpSnapshot p
        clickOrContinue p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        dumpSnapshot p
        typet p "xpath///input[@name='identificator']" <| username ()
        typet p "xpath///input[@name='accessPin']"     <| password ()
        let w = p.WaitForNetworkIdleAsync()
        sleep 1 // I have experienced that without waiting here clicking the "submit" button has no effect
        click p "xpath///button[@type='submit']"
        w |> wait
        dumpSnapshot p
        signedIn <- true

    let ScrapeInvoicesInternal () =
        // we rely on the index here because the list is rebuild and DOM nodes are detached
        let invoice_indexes = queryAll p "xpath///div[contains(@class,'table-row')]" |> Array.mapi (fun i _ -> i)

        let invoices = [
            for index in invoice_indexes do
                let invoice_row =
                    queryAll p "xpath///div[contains(@class,'table-row')]"
                    |> Array.mapi (fun i x -> i, x)
                    |> Array.find (fun (i, _) -> i = index)
                    |> snd

                // the "amount to pay is only available in the table before clicking on a invoice"
                let invoice_row_cells = queryElementAll invoice_row "xpath/./div/div"
                clickSelector "xpath/.//i[contains(@class,'icon-zoom')]" invoice_row // click the magnifier to show details of the invoice

                // some invoices take long to load
                waitTillHTMLRendered p

                let modal = queryFirst p "xpath///div[@class='ModalContent']"
                let modal_rows = queryElementAll modal "xpath/./div[@class='agreementModal']/div"

                p.Keyboard.PressAsync "Escape" |> wait // press Escape so we can get details for next invoice
                sleep 2
                yield invoice_row_cells, modal_rows ]

        invoices

    member this.SignIn() =
        try
            if p = null then
                p <- getPage args
            if signedIn |> not then
                signInInternal ()
        with e ->
            dumpSnapshot p
            raise e

    member private this.SubmitIndicationInternal indication =
        goto p "https://ebok.pgnig.pl/odczyt"
        waitTillHTMLRendered p
        dumpSnapshot p
        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        dumpSnapshot p
        if isTest |> not then
            typet p "xpath///input[@id='reading-0']" (indication |> string)
            click p "xpath///button[contains(text(), 'Zapisz odczyt')]"
            click p "xpath///button[contains(text(), 'Tak')]"
            waitTillHTMLRendered p // make sure the input was accepted
        else
            printfn "Skipping indication submission in test mode"
        dumpSnapshot p

    member this.SubmitIndication indication =
        try
            this.SubmitIndicationInternal indication
        with e ->
            dumpSnapshot p
            raise e

    member private this.ScrapeInvoicesInternal() =
        let getAllTexts (x: IElementHandle) = queryElementAll x "xpath/.//text()" |> Array.map getText

        let parseInvoiceToStrings (before: IElementHandle array, after: IElementHandle array) =
            let before = before |> Array.collect getAllTexts |> Array.map (fun x -> x.Trim()) |> Array.filter (fun x -> x <> "")
            let after  = after  |> Array.collect getAllTexts |> Array.map (fun x -> x.Trim()) |> Array.filter (fun x -> x <> "")
            {
                BeforeClickingOnInvoice = before
                AfterClickingOnInvoice = after
            }

        goto p "https://ebok.pgnig.pl/faktury"
        sleep 2
        // because we use `goto` to navigate instead of buttons on the SPA
        // we need to close the pop-up again
        click p "xpath///i[contains(@class,'icon-close')]"
        sleep 1
        dumpSnapshot p
        ScrapeInvoicesInternal () |> List.map parseInvoiceToStrings

    member this.ScrapeInvoices() =
        try
            this.ScrapeInvoicesInternal()
        with e ->
            dumpSnapshot p
            raise e

    member private this.ScrapeOverpaymentsInternal() =
        goto p "https://ebok.pgnig.pl/umowy"
        sleep 2
        dumpSnapshot p

        let rows = queryAll p "xpath///div[contains(@class,'table-row')]"

        rows
        |> Array.map (fun row ->
            queryElementAll row "xpath/.//div[contains(@class,'columns')]"
            |> Array.map getText
            |> Array.map (fun x -> x.Trim())
            |> Array.filter (fun x -> x <> ""))

    member this.ScrapeOverpayments() =
        try
            this.ScrapeOverpaymentsInternal()
        with e ->
            dumpSnapshot p
            raise e

    member this.GetP() = p

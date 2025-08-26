namespace clients

open PuppeteerSharp

module BaseClient =
    open Utils

    [<AbstractClass>]
    type BaseClient(usr, pwd, args, p, isSignedIn, isTest) =

        let usr          = usr
        let pwd          = pwd
        let args         = args
        let isSignedIn   = isSignedIn
        let isTest       = isTest

        let mutable p = p

        member this.Page
            with get() = p
            and set(value) = p <- value

        member this.InitializePage() =
            printfn "downloading chromium"
            let bf = new BrowserFetcher()
            bf.DownloadAsync() |> wait
            let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions(), Args = args)
            let b = Puppeteer.LaunchAsync(l_options).Result
            p <- b.PagesAsync() |> fun x -> x.Result |> Array.exactlyOne

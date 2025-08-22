#load "c:/git/prelude/prelude.fsx"
#load "local_prelude.fsx"

Prelude.WorkingDirectorySetter.SetToMe()

open System

// --- header --- //

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "pgn.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.PGNIG

#load "pgn.fs"
open clients.PGNIG

let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD")

//c.SingIn()
let mutable p = c.GetP()


p <-
    let arg = [| "--disable-notifications" |]
    let opt = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions(), Args = arg)
    let brw = Puppeteer.LaunchAsync opt |> runSync
    brw.PagesAsync() |> runSync |> Array.exactlyOne

let w = p.WaitForNetworkIdleAsync()
p.GoToAsync "https://ebok.pgnig.pl/" |> wait
printf "Waiting for page to load... "
w |> wait
printfn "done"
click p "xpath///button[text()='Odrzuć wszystkie']"
sleep 1
click p "xpath///i[contains(@class,'icon-close')]"
sleep 1

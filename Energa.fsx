#load "c:/git/prelude/prelude.fsx"

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "Energa.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.Energa

let username () = System.Environment.GetEnvironmentVariable("ENERGA_USERNAME")
let password () = System.Environment.GetEnvironmentVariable("ENERGA_PASSWORD")

let c = EnergaClient(username, password)
c.SingIn()

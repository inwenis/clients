#load "c:/git/prelude/prelude.fsx"

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/Energa.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.Energa

let c = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD")
c.SingIn()

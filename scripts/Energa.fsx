#load "c:/git/prelude/prelude.fsx"

#r "nuget: FSharp.Data, 6.6"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/Energa.fs"

open System.IO
open System
open PuppeteerSharp

open Utils
open Energa

let c = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD")
c.SignIn()

c.SubmitIndication("105", 1234)

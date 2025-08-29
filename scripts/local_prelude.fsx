#r "nuget: PuppeteerSharp, 20.2.2"

open PuppeteerSharp

fsi.AddPrinter(fun (x:JSHandle) -> x.EvaluateFunctionAsync<string>("el => el.outerHTML").Result)

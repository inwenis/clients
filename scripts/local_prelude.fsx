#r "nuget: PuppeteerSharp, 18.0.3"

open PuppeteerSharp

fsi.AddPrinter(fun (x:JSHandle) -> x.EvaluateFunctionAsync<string>("el => el.outerHTML").Result)

# Clients

This repo contains clients I use to automate settling utilities.

# How to use it?

The clients are meant to be referenced via paket's [GitHub dependencies](https://fsprojects.github.io/Paket/github-dependencies.html) and used in an FSI (.fsx) environment.

# Snapshots

The clients occasionally save snapshots to `./snapshots/page_{timestamp}.mhtml`.
They should be ignored in your `.gitignore`.
They are meant for debugging if the client encounters an error, for e.x. when the page's has a different format than we expected.

# TODO

- consider better colorful logging - highlight xpath syntax
- add C# config tutorial
- add F# example
- add example using https://fsprojects.github.io/Paket/fsi-integration.html
- close clients when done, maybe implement IDisposable?
- make up your mind if you want to use wait for selector or query selector
- add error handling in helpers functions
- getP only returns something after you're signed in
- args are irrelevant in clients when page is given
- unify if clients automatically login in or you have to do it explicitly
- add a "keep being logged in background threat"
- think if dumping page in Alior clients poses a security risk

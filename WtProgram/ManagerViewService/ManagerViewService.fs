namespace Bemo
open System.Threading
open System.Windows.Forms

type ManagerViewService() =
    // The settings window runs on its own UI thread, so it stays responsive while the main thread
    // handles shell events (and doesn't delay them). The thread lives as long as the program, so
    // anything the settings window sets up on it keeps working after the window is closed.
    let invoker = lazy (
        let ready = new ManualResetEvent(false)
        let invoker = ref null
        let thread = Thread(fun () ->
            invoker := InvokerService.invoker
            ready.Set() |> ignore
            Application.Run())
        thread.SetApartmentState(ApartmentState.STA)
        thread.IsBackground <- true
        thread.Name <- "Settings UI"
        thread.Start()
        ready.WaitOne() |> ignore
        invoker.Value)

    // One window, hidden rather than destroyed when closed: building it is the slow part.
    // Only touched on the settings thread.
    let mutable form : DesktopManagerForm option = None

    let withForm f =
        invoker.Force().asyncInvoke <| fun () ->
            let current =
                match form with
                | Some(current) when current.isDisposed.not -> current
                | _ ->
                    let created = DesktopManagerForm()
                    form <- Some(created)
                    created
            f current

    interface IManagerView with
        member x.show() =
            withForm <| fun form -> form.show()

        member x.show(view) =
            withForm <| fun form -> form.showView(view)

namespace Bemo
open System
open System.Drawing
open System.Reflection
open System.Collections.Generic

type ServiceAsyncResult() as this =
    let returnInvoker = InvokerService.invoker
    let mutable cachedResult = None
    let mutable cachedfCompleted = None
    let mutable completed = false

    member this.complete(result) =
        returnInvoker.asyncInvoke <| fun() ->
            cachedResult <- Some(result)
            this.tryToComplete()

    member this.tryToComplete() =
        if completed.not && cachedResult.IsSome && cachedfCompleted.IsSome then
            completed <- true
            cachedfCompleted.Value(cachedResult.Value)

    interface IServiceAsyncResult with
        member x.onCompleted fCompleted =
            returnInvoker.asyncInvoke <| fun() ->
                cachedfCompleted <- Some(fCompleted)
                this.tryToComplete()

// DispatchProxy creates a subclass of this type that implements 'a, so it needs a
// parameterless constructor; the wrapped service is passed in afterwards via init.
type ServiceProxy<'a>() =
    inherit DispatchProxy()
    let attributeCache = new Dictionary<int, ServiceMethodAttribute>()

    let invoker = InvokerService.invoker
    let mutable service = Unchecked.defaultof<'a>

    static member create(service:'a) =
        let proxy = DispatchProxy.Create<'a, ServiceProxy<'a>>()
        (box proxy :?> ServiceProxy<'a>).init(service)
        proxy

    member private this.init(s:'a) = service <- s

    member private this.serviceMethodAttributeCached(mi:MethodInfo) =
        let key = mi.MetadataToken
        lock this <| fun() ->
            if attributeCache.ContainsKey(key).not then
                let attributes = List2(mi.GetCustomAttributes(typeof<ServiceMethodAttribute>, true))
                let sma = attributes.map(fun(attr) -> attr.cast<ServiceMethodAttribute>()).tryHead.def(ServiceMethodAttribute())
                attributeCache.Add(key, sma)
            attributeCache.Item(key)

    member private this.invokeMethod(mi:MethodInfo, args:obj[]) =
        mi.Invoke(service, args)

    member private this.isUnitReturnType(mi:MethodInfo) =
        mi.ReturnType = typeof<unit> || mi.ReturnType = typeof<Void>

    member private this.doSyncInvoke(mi, args) =
        invoker.invoke <| fun() ->
            this.invokeMethod(mi, args)

    member private this.doAsyncInvoke(mi, args) =  
        let asyncResult = ServiceAsyncResult()

        invoker.asyncInvoke <| fun() -> 
            let result = this.invokeMethod(mi, args)
            asyncResult.complete(result)

        if this.isUnitReturnType(mi) then null else box(asyncResult)

    override this.Invoke(mi, args) =
        let sma = this.serviceMethodAttributeCached mi
        if sma.async then
            this.doAsyncInvoke(mi, args)
        else
            this.doSyncInvoke(mi, args)

type ServiceProvider() =
    [<DefaultValue>]
    [<ThreadStatic>]
    static val mutable private _localServices : Dictionary<Type, obj>

    let services = new Dictionary<Type, obj>()
    
    static member localServices
        with get() =
            if ServiceProvider._localServices = null then
                ServiceProvider._localServices <- new Dictionary<Type, obj>()
            ServiceProvider._localServices

    member this.register(service:'a, wrap) =
        let service = 
            if wrap then
                box(ServiceProxy<'a>.create(service))
            else
                box(unbox<'a>(service))
        services.Add(typeof<'a>, service)

    member this.register(service:'a) = this.register(service, true)

    member this.registerLocal(service:'a) = 
        ServiceProvider.localServices.Add(typeof<'a>, service)

    member this.get<'a>() =
        let t = typeof<'a>
        let service = 
            if ServiceProvider.localServices.ContainsKey(t) then
                ServiceProvider.localServices.Item(t)
            else
                services.Item(t)
        unbox<'a>(service)

    member this.has<'a>() =
        let t = typeof<'a>
        ServiceProvider.localServices.ContainsKey(t) || services.ContainsKey(t)

type WtServiceProvider() =
    inherit ServiceProvider()
    member this.program = this.get<IProgram>()
    member this.desktop = this.get<IDesktop>()
    member this.managerView = this.get<IManagerView>()
    member this.filter = this.get<IFilterService>()
    member this.settings = this.get<ISettings>()
    member this.lm = this.get<ILicenseManager>()
    member this.dragDrop = this.get<IDragDrop>()
    member this.openResource(name) = Assembly.GetEntryAssembly().GetManifestResourceStream(name)
    member this.openIcon(name) = new Icon(this.openResource(name))
    member this.openImage(name) = System.Drawing.Image.FromStream(this.openResource(name))

[<AutoOpen>]
module GS =
    let Services = WtServiceProvider()
    
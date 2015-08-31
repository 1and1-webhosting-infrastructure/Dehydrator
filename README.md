# Dehydrator

Dehydrator helps you combine ORMs like [Entity Framework](https://msdn.microsoft.com/data/ef.aspx) with REST service frameworks like [WebAPI](http://www.asp.net/web-api).

By stripping navigational references in your entities down to only their IDs their serialized representation no longer contains redundant or cyclic data. When new or modified deserialized data needs to be persisted Dehydrator takes care of resolving the reference IDs again.

NuGet packages:
* [Dehydrator](https://www.nuget.org/packages/Dehydrator/)
* [Dehydrator.EntityFramework](https://www.nuget.org/packages/Dehydrator.EntityFramework/)
* [Dehydrator.WebApi](https://www.nuget.org/packages/Dehydrator.WebApi/)

While Dehydrator is designed primarily for use with Entity Framework and WebAPI you can also use the core package (`Dehydrator`) with any other ORM and REST framework. You'll just need to implement the parts from the other packages yourself, which should be pretty straightforward.


## Usecase sample

We'll use this simple POCO (Plain old CLR object) class modeling software packages and their dependencies as an example:
```cs
public class Package : IEntity
{
  public long Id { get; set; }
  public string Name { get; set; }

  [Dehydrate]
  public virtual ICollection<Package> Dependencies { get; set; }
}
```

Without Dehydrator your REST service might return something like this for `GET /packages/1`:
```javascript
{
  "Id": 1,
  "Name": "AwesomeApp",
  "Dependencies":
  [
    {
      "Id": 2,
      "Name": "AwesomeLib",
      "Dependencies": []
    }
  ]
}
```
The entire `AwesomeLib` reference is inlined. This duplicates content from `/packages/2` and complicates `POST` and `PUT` calls. Are references expected to be existing entities? Are they created/modified together with the parent entity if no matching entity exists yet?

With Dehydrator the same entity would be serialized as:
```javascript
{
  "Id": 1,
  "Name": "AwesomeApp",
  "Dependencies":
  [
    {"Id": 2}
  ]
}
```
This removes any potential duplication and ambiguity.


## Getting started

### Data model
Install the `Dehydrator` NuGet package in the project holding your data model. Make all your entity classes either implement `IEntity` or derive from `Entity`. Mark any reference properties you wish to have dehydrated with `[Dehydrate]`. You can now use the `.DehydrateReferences()` extension method to dehydrate references down to only their IDs and  and `.ResolveReferences()` to resolve/restore them again.

If you want to have a property resolved but not dehydrated (e.g., incoming data is dehydrated and needs to be resolved but outgoing data should not be dehydrated) use `[Resolve]` instead. Combining `[Dehydrate]` and `[Resolve]` is not necessary since `[Dehydrate]` implies `[Resolve]`.

Resolving requires an `IRepositoryFactory`, which represents a storage backend such as a database and provides `IRepository<>` instances for specific entity types.

### Entity Framework
Install the `Dehydrator.EntityFramework` NuGet package in the project you use for database access via Entity Framework. This provides `DbRepositoryFactory`, an `IRepositoryFactory` implementation based on Entity Framework's `DbSet`.

Use the `DehydratingRepositoryFactory` decorator to wrap your `IRepositoryFactory` implementation (`DbRepositoryFactory` or a custom implementation). This decorator transparently dehydrates and resolve references when loading and saving entities.

Make sure to make any reference/navigational properties `virtual` to enable Entity Framework's lazy loading feature.

### WebAPI
To avoid unnecessary noise in dehydrated JSON output you should add the following line to your `WebApiConfig.Register()` method:
```cs
config.Formatters.JsonFormatter.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
```

Install the `Dehydrator.WebApi` NuGet package in your WebAPI project. You can then derive your controller classes from `CrudController` or `AsyncCrudController` which take an `IRepository<>` as a constructor argument. Use instances created by `DehydratingRepositoryFactory`. The best way to acheive this is dependency injection. See below for Unity.

If you choose to use the `CrudController` or `AsyncCrudController` base classes you must add the following line to your `WebApiConfig.Register()` method:
```cs
config.MapHttpAttributeRoutes(new InheritanceRouteProvider());
```

You can also build your own controllers using `IRepository<>`s directly without using the `Dehydrator.WebApi` package.

### Unity
If you wish to use the [Unity Application Block](https://unity.codeplex.com/) for dependency injection in your WebAPI application consider using the following code as a template for registering the appropriate Dehydrator types:
```cs
public static IUnityContainer InitContainer()
{
  return new UnityContainer()
    .RegisterType<DbContext, YourOwnDbContext>()
    .RegisterDehydratedDbRepository()
    .EnableRepositoryFactory();
}

private static IUnityContainer RegisterDehydratedDbRepository(this IUnityContainer container)
{
  return container.RegisterType<IRepositoryFactory>(new InjectionFactory(c =>
    new DehydratingRepositoryFactory(new DbRepositoryFactory(c.Resolve<DbContext>()))));
}

private static IUnityContainer EnableRepositoryFactory(this IUnityContainer container)
{
  return container.RegisterType(typeof(IRepository<>), new InjectionFactory((c, t, s) =>
    c.Resolve<IRepositoryFactory>().Create(t.GetGenericArguments()[0])));
}
```

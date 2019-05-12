# Notes

For general information on implementing a view engine:
* https://www.davepaquette.com/archive/2016/11/22/creating-a-new-view-engine-in-asp-net-core.aspx
* https://github.com/AspNetMonsters/pugzor/

The [Razor view engine](https://github.com/aspnet/AspNetCore) implementation was helpful in developing NVue.

### Specific files of importance:
* [RuntimeViewCompiler.cs](https://github.com/aspnet/AspNetCore/blob/1b500858354efe26493af632bf0e3f5462dc6246/src/Mvc/Mvc.Razor.RuntimeCompilation/src/RuntimeViewCompiler.cs) - most important file
* [PropertyActivator.cs](https://github.com/aspnet/AspNetCore/blob/3c09d644cccdb21801f7a79e1188a1a1212de5d9/src/Shared/PropertyActivator/PropertyActivator.cs) - second most important file
* [RuntimeViewCompilerTest.cs](https://github.com/aspnet/AspNetCore/blob/1b500858354efe26493af632bf0e3f5462dc6246/src/Mvc/Mvc.Razor.RuntimeCompilation/test/RuntimeViewCompilerTest.cs)
* [DefaultRazorEngine.cs](https://github.com/aspnet/Razor/blob/8d629371bfc8a80b2bca2660106f194ccffd0a21/src/Microsoft.AspNetCore.Razor.Language/DefaultRazorEngine.cs)

### Other resources

* https://github.com/ktsn/vue-ast-explorer
* https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples
* https://github.com/vuejs/vue/blob/dev/src/compiler/parser/index.js  - vue compiler


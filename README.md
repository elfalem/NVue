# NVue

An experimental view rendering engine for ASP.NET Core that's based on the Vue.js [template syntax](https://vuejs.org/v2/guide/syntax.html). It is an alternative to the [Razor](https://docs.microsoft.com/en-us/aspnet/core/mvc/views/razor?view=aspnetcore-2.2) view engine.

To be clear, there is no JavaScript involved. Only the HTML-based declarative syntax of Vue.js is used with C# expressions.

## Syntax Examples

NVue files contain template markup in a top level `<template>` tag.

String interpolation:
````
<template>
    <div>Name: {{Name}}</div>
</template>
````

Iterate through a list of items:
````
<template>
    <div v-for="item in Items">
        {{item.Id}} - {{item.Name}}
    </div>
</template>
````

Expressions can also be bound to attributes if they start with a colon. (Alternatively you can use the more verbose `v-bind:` syntax)
````
<template>
    <div v-for="item in Items">
        <span :id="item.Id">{{item.Name}}</span>
    </div>
</template>
````

Conditional blocks:
````
<template>
    <div v-if="User.IsLoggedIn">
        Welcome back!
    </div>
    <div v-else>
        Please sign in.
    </div>
</template>
````
`v-else-if` is also supported. `v-show` has the same behavior as `v-if`.

You can also use `<template>` tags within a template to control flow without the tags themselves being rendered:
````
<template>
    <template v-if="User.IsLoggedIn">
        Welcome back!
    </template>
    <template v-else>
        Please sign in.
    </template>
</template>
````

## Layouts

Similar to layout sections in Razor, you can have a base layout template that's used in rendering multiple views. This is supported using the concept of slots.

The following example layout file has two slots. A default one, and another one named `sidebar`. Different views can populate these slots with their own content. If no content is provided for a slot, the default content from a layout file will be used. Slot names are not case sensitive.

````
<template>
    <html>
        <body>
            <div class="header">
                Site Name
            </div>
            <div class="navigation">
                <a href="/">Home</a>
                <a href="/about">About</a>
            </div>
            <div class="main">
                <slot></slot>
            </div>
            <div class="sidebar">
                <slot name="sidebar">Default Sidebar Content.</slot>
            </div>
        </body>
    </html>
</template>
````

The following example template provides content for the slots.
````
<template>
    This is the main content.
    <template v-slot:sidebar>
        This goes in the sidebar.
    </template>
    <div>
        This is a continuation of the main content.
    </div>
</template>
````

By default, NVue looks for a layout file called `_Layout.nvue`. You can specify a different layout by setting the `layout` attribute on the root `template` tag. (This is a deviation from the Vue.js template syntax which has no concept of layouts.)

For example, to use the layout template `AltLayout.nvue`:

`<template layout="AltLayout">`

## Scripts

Instead of including long inline C# expressions in the HTML template, you can add them in a script section of type `text/csharp`:

````
<template>
    <div v-for="var post in Posts">
        {{post}} - length: {{CountWords(post)}} words
    </div>
</template>

<script type="text/csharp">
string wordSeparator = " ";

int CountWords(string content){
    return content.Split(wordSeparator).Length;
}
</script>
````

## Usage Walkthrough
Assuming you have the .NET Core Runtime and SDK installed, create a sample ASP.NET MVC project.

````
mkdir test-nvue-project
cd test-nvue-project/
dotnet new mvc
````

To verify, you can run the application and browse it at https://localhost:5001

`dotnet run`

Add and use the NVue [NuGet package](https://www.nuget.org/packages/NVue/) (first stop the server with Ctrl+C if it's running).

`dotnet add package NVue`

In `Startup.cs`, add the following using statement:

`using NVue.Core;`

Also in `ConfigureServices()` method of `Startup.cs`, add the view engine:

````
services.AddMvc().AddViewOptions(options => {
        options.ViewEngines.Add(new NVueViewEngine());
    });
````

Add a new action in `HomeController.cs`:

````
public IActionResult Foo(){
    ViewData["Title"] = "Hello World!";
    ViewData["Continents"] = new List<string>{
        "Africa",
        "Antarctica",
        "Asia",
        "Australia",
        "Europe",
        "North America",
        "South America"
    };
    return View();
}
````

Create the view file `Home/Foo.nvue` and add the following template:

````
<template>
    <h3>{{Title}}</h3>
    <ul>
        <li v-for="var continent in Continents">Hello {{continent}}</li>
    </ul>
</template>
````

Start the application and navigate to https://localhost:5001/Home/Foo

`dotnet run`

You should see content similar to the following:

````
Hello World!

    Hello Africa
    Hello Antarctica
    Hello Asia
    Hello Australia
    Hello Europe
    Hello North America
    Hello South America
````

## Limitations

There are no equivalents to the Razor concepts of Tag Helpers, Partial Views, and View Components.

## Final Notes
Please note that this is currently a proof of concept. I'd like to hear about your experience if you give it a try. Also, pull requests are welcome!
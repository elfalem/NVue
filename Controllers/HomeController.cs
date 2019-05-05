using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Blog;
using Microsoft.AspNetCore.Mvc;
using NVue.Models;

namespace NVue.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Foo(){
            ViewData["Message"] = "Bye World";
            ViewData["Posts"] = new List<Post>{
                new Post{
                    Id = 1,
                    Title = "one"
                    },
                new Post{
                    Id = 2,
                    Title = "two"
                }
            };
            ViewData["Comments"] = new List<string>{"a", "b"};
            // ViewData["Sample"] = new Dictionary<List<string>, List<string>>();
            // ViewData["ArraySample"] = new string[]{};
            // ViewData["Number"] = 5;

            // ViewData["ErrorView"] = new ErrorViewModel();
            // ViewData["Time"] = DateTime.Now;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NVue.Sample.Models;

namespace NVue.Sample.Controllers
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

        public IActionResult Blog(){
            var blog = new Blog{
                Name = "Foobar",
                Subtitle = "A sample blog."
            };

            var postOne = new Post{
                Id = 1,
                Author = "John Doe",
                Published = DateTime.Now,
                Title = "My first post",
                Content = "Hello World! This is my first blog post. I plan to write about many things. I hope you like it.",
                Comments = new List<Comment>{
                    new Comment{
                        Id = 93,
                        Author = null,
                        Published = DateTime.Now,
                        Content = "First"
                    },
                    new Comment{
                        Id = 9834,
                        Author = "Sally",
                        Published = DateTime.Now,
                        Content = "Looking forward to your posts!"
                    }
                }
            };

            var postTwo = new Post{
                Id = 2,
                Author = "John Doe",
                Published = DateTime.Now,
                Title = "Update",
                Content = "It has been a long time since I last posted. I was busy with many projects. Stay tuned for updates.",
                Comments = new List<Comment>()
            };

            blog.Posts = new List<Post>{
                postOne,
                postTwo
            };

            ViewData["Title"] = "My Blog";
            ViewData["Blog"] = blog;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

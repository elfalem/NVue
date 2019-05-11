using System;
using System.Collections.Generic;

namespace NVue.Sample.Models
{
    public class Post
    {
        public int Id {get; set;}
        public string Author {get; set;}
        public DateTime Published {get; set;}
        public string Title {get; set;}
        public string Content {get; set;}
        public List<Comment> Comments {get; set;}
    }
}
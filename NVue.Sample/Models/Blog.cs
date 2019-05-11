using System.Collections.Generic;

namespace NVue.Sample.Models{
    public class Blog{
        public string Name {get; set;}
        public string Subtitle {get; set;}
        public List<Post> Posts {get; set;}
    }
}
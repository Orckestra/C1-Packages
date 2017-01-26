using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Web.Hosting;

namespace Orckestra.Web.Html.Animate
{
    public static class InlineMethodFunction
    {
        public static IEnumerable<string> GetAnimationNames()
        {
            var pathtocss = HostingEnvironment.MapPath("/Frontend/Orckestra/Web/Html/Animate/animate.css");
            if (File.Exists(pathtocss))
            {
                var text = File.ReadAllText(pathtocss);
                var matches = Regex.Matches(text, @"@keyframes ([A-Za-z0-9\-]+) {");

                foreach (Match m in matches)
                {
                    yield return m.Groups[1].Value;
                }
            }
        }
    }
}
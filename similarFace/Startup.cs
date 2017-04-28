using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(similarFace.Startup))]
namespace similarFace
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}

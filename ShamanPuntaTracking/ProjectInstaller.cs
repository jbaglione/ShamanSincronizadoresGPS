using System.ComponentModel;
using System.Configuration.Install;

namespace ShamanPuntaTracking
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}

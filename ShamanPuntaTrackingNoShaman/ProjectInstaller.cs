using System.ComponentModel;
using System.Configuration.Install;

namespace ShamanPuntaTrackingNoShaman
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

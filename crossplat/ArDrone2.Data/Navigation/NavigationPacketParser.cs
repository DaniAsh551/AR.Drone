using ArDrone2.Data.Navigation.Native;

namespace ArDrone2.Data.Navigation
{
    public class NavigationPacketParser
    {
        public static bool TryParse(ref NavigationPacket packet, out NavigationData navigationData)
        {
            navigationData = new NavigationData();
            NavdataBag navdataBag;
            if (NavdataBagParser.TryParse(ref packet, out navdataBag))
            {
                navigationData = NavdataConverter.ToNavigationData(navdataBag);
                return true;
            }
            return false;
        }
    }
}
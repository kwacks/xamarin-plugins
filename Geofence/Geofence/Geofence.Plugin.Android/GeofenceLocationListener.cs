using System.Diagnostics;
using Android.Locations;
using Java.Lang;
using ILocationListener = Android.Gms.Location.ILocationListener;

namespace Geofence.Plugin
{
    /// <summary>
    /// GeofenceLocationListener class
    /// Listens to location updates
    /// </summary>
    public class GeofenceLocationListener : Object,ILocationListener
    {
        private static GeofenceLocationListener sharedInstance = new GeofenceLocationListener();

        /// <summary>
        /// Location listener instance
        /// </summary>
        public static GeofenceLocationListener SharedInstance { get { return sharedInstance; } }
        
        private GeofenceLocationListener()
        {

        }
        void ILocationListener.OnLocationChanged(Location location)
        {
            //Location Updated
            Debug.WriteLine("{0} - {1}: {2},{3}", CrossGeofence.Id, "Location Update", location.Latitude, location.Longitude);
            ((GeofenceImplementation)CrossGeofence.Current).SetLastKnownLocation(location);
            
        }
    }
}

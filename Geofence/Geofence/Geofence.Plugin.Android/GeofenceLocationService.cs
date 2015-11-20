using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Debug = System.Diagnostics.Debug;
using ILocationListener = Android.Gms.Location.ILocationListener;

namespace Geofence.Plugin
{
    /// <summary>
    /// GeofenceLocationService
    /// </summary>
    [Service]
    public class GeofenceLocationService : Service, ILocationListener
    {
        /// <summary>
        /// Location Service OnCreate method
        /// </summary>
        public override void OnCreate()
        {
           /* mLocationRequest = Android.Gms.Location.LocationRequest.create();
            mLocationRequest.setInterval(CommonUtils.UPDATE_INTERVAL_IN_MILLISECONDS);
            mLocationRequest.setPriority(LocationRequest.PRIORITY_HIGH_ACCURACY);
            mLocationRequest.setFastestInterval(CommonUtils.FAST_INTERVAL_CEILING_IN_MILLISECONDS);
            mLocationClient = new LocationClient(getApplicationContext(), this, this);
            mLocationClient.connect();*/
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public override IBinder OnBind(Intent intent)
        {
          return null;
        }


        /// <summary>
        /// Location changed method
        /// </summary>
        /// <param name="location"></param>
        public void OnLocationChanged(Location location)
        {
            //Location Updated
            Debug.WriteLine("{0} - {1}: {2},{3}", CrossGeofence.Id, "Location Update", location.Latitude, location.Longitude);
       
        }
    }
}

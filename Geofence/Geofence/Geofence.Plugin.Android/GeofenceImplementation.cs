using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Locations;
using Android.OS;
using Android.Provider;
using Geofence.Plugin.Abstractions;
using Java.Interop;
using Debug = System.Diagnostics.Debug;
using Exception = Java.Lang.Exception;
using IGeofence = Geofence.Plugin.Abstractions.IGeofence;
using Object = Java.Lang.Object;

namespace Geofence.Plugin
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    /// 
    public class GeofenceImplementation : Object, IGeofence, GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener, IResultCallback
    {

      internal const string GeoReceiverAction = "ACTION_RECEIVE_GEOFENCE";
  
      private Dictionary<string,GeofenceCircularRegion> mRegions= GeofenceStore.SharedInstance.GetAll();

      private  Dictionary<string, GeofenceResult> mGeofenceResults= new Dictionary<string, GeofenceResult>();

      private GeofenceLocation lastKnownGeofenceLocation;

      private PendingIntent mGeofencePendingIntent;

      private GoogleApiClient mGoogleApiClient;

      // Defines the allowable request types
	  internal enum RequestType { Add, Update, Delete, Clear, Default }
      /// <summary>
      /// Get all regions been monitored
      /// </summary>
      public IReadOnlyDictionary<string, GeofenceCircularRegion> Regions { get { return mRegions; } }
      /// <summary>
      /// Get geofence state change results.
      /// </summary>
      public IReadOnlyDictionary<string, GeofenceResult> GeofenceResults { get { return mGeofenceResults; } }

      private IList<string> mRequestedRegionIdentifiers;
      /// <summary>
      /// Get last known location
      /// </summary>
      public GeofenceLocation LastKnownLocation { get { return lastKnownGeofenceLocation; }  }

      internal RequestType CurrentRequestType { get; set; }
  
      /// <summary>
      /// Checks if region are been monitored
      /// </summary>
      public bool IsMonitoring { get { return mRegions.Count > 0; } }
      
        //IsMonitoring?RequestType.Add:

      private PendingIntent GeofenceTransitionPendingIntent
      {
          get
          {
              // If the PendingIntent already exists
              if (mGeofencePendingIntent==null)
              {

                  //var intent = new Intent(Android.App.Application.Context, typeof(GeofenceBroadcastReceiver));
                  // intent.SetAction(string.Format("{0}.{1}", Android.App.Application.Context.PackageName, GeoReceiverAction));
                  var intent = new Intent(string.Format("{0}.{1}", Application.Context.PackageName, GeoReceiverAction));
                  mGeofencePendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.UpdateCurrent);
              }
              return mGeofencePendingIntent;
          }
      }
      /// <summary>
      /// Android Geofence plugin implementation
      /// </summary>
      public GeofenceImplementation()
	  {

          //Check if location services are enabled
          if (!((LocationManager)Application.Context.GetSystemService(Context.LocationService)).IsProviderEnabled(LocationManager.GpsProvider))
          {
              string message = string.Format("{0} - {1}", CrossGeofence.Id, "You need to enable Location Services");
              Debug.WriteLine(message);
              CrossGeofence.GeofenceListener.OnError(message);
              return;
          }

          CurrentRequestType = RequestType.Default;

          InitializeGoogleAPI();

          if(IsMonitoring)
          { 
             StartMonitoring(Regions.Values.ToList());
             Debug.WriteLine("{0} - {1}", CrossGeofence.Id, "Monitoring was restored");
          }
          
         
         
    

	  }
      /// <summary>
      /// Starts monitoring specified region
      /// </summary>
      /// <param name="region"></param>
      public void StartMonitoring(GeofenceCircularRegion region)
      {
          /*if (IsMonitoring && mGoogleApiClient.IsConnected)
          {
              Android.Gms.Location.LocationServices.GeofencingApi.RemoveGeofences(mGoogleApiClient, GeofenceTransitionPendingIntent).SetResultCallback(this);
          }*/

          if (!mRegions.ContainsKey(region.Id))
          {
              mRegions.Add(region.Id, region);
          }


          RequestMonitoringStart();
      }
      void RequestMonitoringStart()
      {
          //If connected to google play services then add regions
          if (mGoogleApiClient.IsConnected)
          {

              AddGeofences();


          }
          else
          {
              //If not connection then connect
              if (!mGoogleApiClient.IsConnecting)
              {
                  mGoogleApiClient.Connect();

              }
              //Request to add geofence regions once connected
              CurrentRequestType = RequestType.Add;

          }
      }
      /// <summary>
      /// Starts geofence monitoring on specified regions
      /// </summary>
      /// <param name="regions"></param>
      public void StartMonitoring(IList<GeofenceCircularRegion> regions)
      {
         /* if (IsMonitoring && mGoogleApiClient.IsConnected)
          {
              Android.Gms.Location.LocationServices.GeofencingApi.RemoveGeofences(mGoogleApiClient, GeofenceTransitionPendingIntent).SetResultCallback(this);
          }*/

          foreach (var region in regions)
          {
              if (!mRegions.ContainsKey(region.Id))
              {
                  mRegions.Add(region.Id, region);
              }
          }

          RequestMonitoringStart();

      }
  
      
        internal void AddGeofenceResult(string identifier)
        {
             mGeofenceResults.Add(identifier, new GeofenceResult
             {
                            RegionId = identifier,
                            Transition = GeofenceTransition.Unknown
              });
        }
        private void AddGeofences()
        {
            try
            {
                List<Android.Gms.Location.IGeofence> geofenceList = new List<Android.Gms.Location.IGeofence>();
                var regions = Regions.Values;
                foreach (GeofenceCircularRegion region in regions)
                {
                    int transitionTypes = 0;
                    
                    if(region.NotifyOnStay)
                    {
                        transitionTypes |= Android.Gms.Location.Geofence.GeofenceTransitionDwell;
                    }

                    if(region.NotifyOnEntry)
                    {
                        transitionTypes |= Android.Gms.Location.Geofence.GeofenceTransitionEnter;
                    }

                    if (region.NotifyOnExit)
                    {
                        transitionTypes |= Android.Gms.Location.Geofence.GeofenceTransitionExit;
                    }
                    
                    if(transitionTypes != 0)
                    {

                        geofenceList.Add(new GeofenceBuilder()
                       .SetRequestId(region.Id)
                       .SetCircularRegion(region.Latitude, region.Longitude, (float)region.Radius)
                       .SetLoiteringDelay((int)region.StayedInThresholdDuration.TotalMilliseconds)
                       //.SetNotificationResponsiveness(mNotificationResponsivness)
                       .SetExpirationDuration(Android.Gms.Location.Geofence.NeverExpire)
                       .SetTransitionTypes(transitionTypes)
                       .Build());

                        if (GeofenceStore.SharedInstance.Get(region.Id) == null)
                        {
                            GeofenceStore.SharedInstance.Save(region);
                        }
                        CrossGeofence.GeofenceListener.OnMonitoringStarted(region.Id);

                    }
                  
                }
               
               if(geofenceList.Count>0)
               {
                   GeofencingRequest request = new GeofencingRequest.Builder().SetInitialTrigger(GeofencingRequest.InitialTriggerEnter).AddGeofences(geofenceList).Build();

                   LocationServices.GeofencingApi.AddGeofences(mGoogleApiClient, request, GeofenceTransitionPendingIntent).SetResultCallback(this);

                   CurrentRequestType = RequestType.Default;
               }
              

            }catch(Exception ex1)
            {
                string message = string.Format("{0} - Error: {1}", CrossGeofence.Id, ex1);
                Debug.WriteLine(message);
                CrossGeofence.GeofenceListener.OnError(message);
            }
            catch (System.Exception ex2)
            {
                string message = string.Format("{0} - Error: {1}", CrossGeofence.Id, ex2.ToString());
                Debug.WriteLine(message);
                CrossGeofence.GeofenceListener.OnError(message);
            }
            
        }


        /// <summary>
        /// Stops monitoring a specific geofence region
        /// </summary>
        /// <param name="regionIdentifier"></param>
        public void StopMonitoring(string regionIdentifier)
        {

            StopMonitoring(new List<string> { regionIdentifier });
        }

        internal void OnMonitoringRemoval()
        {
            if (mRegions.Count == 0)
            {

                CrossGeofence.GeofenceListener.OnMonitoringStopped();

                StopLocationUpdates();


                mGoogleApiClient.Disconnect();
            }
        }
        private void RemoveGeofences(IList<string> regionIdentifiers)
        {
            foreach (string identifier in regionIdentifiers)
            {
                //Remove this region from regions dictionary and results
                RemoveRegion(identifier);
                //Remove from persistent store
                GeofenceStore.SharedInstance.Remove(identifier);
                //Notify monitoring was stopped
                CrossGeofence.GeofenceListener.OnMonitoringStopped(identifier);
            }

            //Stop Monitoring
            LocationServices.GeofencingApi.RemoveGeofences(mGoogleApiClient, regionIdentifiers).SetResultCallback(this);

            //Check if there are still regions
            OnMonitoringRemoval();
        }
        /// <summary>
        /// Stops monitoring specified geofence regions
        /// </summary>
        public void StopMonitoring(IList<string> regionIdentifiers)
        {
            mRequestedRegionIdentifiers = regionIdentifiers;

            if (IsMonitoring && mGoogleApiClient.IsConnected)
            {
                RemoveGeofences(regionIdentifiers);
            }
            else
            {
                //If not connection then connect
                if (!mGoogleApiClient.IsConnecting)
                {
                    mGoogleApiClient.Connect();

                }
                //Request to add geofence regions once connected
                CurrentRequestType = RequestType.Delete;
            }
            //

        }
        /// <summary>
        /// Stops monitoring all geofence regions
        /// </summary>
        public void StopMonitoringAllRegions()
        {
            if (IsMonitoring && mGoogleApiClient.IsConnected)
            {
                RemoveGeofences();
            }
            else
            {
                //If not connection then connect
                if (!mGoogleApiClient.IsConnecting)
                {
                    mGoogleApiClient.Connect();

                }
                //Request to add geofence regions once connected
                CurrentRequestType = RequestType.Clear;
            }
            

        }
        private void RemoveGeofences()
        {
            GeofenceStore.SharedInstance.RemoveAll();
            mRegions.Clear();
            mGeofenceResults.Clear();
            LocationServices.GeofencingApi.RemoveGeofences(mGoogleApiClient, GeofenceTransitionPendingIntent).SetResultCallback(this);
            StopLocationUpdates();
            mGoogleApiClient.Disconnect();
            CrossGeofence.GeofenceListener.OnMonitoringStopped();
        }

        private void InitializeGoogleAPI()
        {
            int queryResult = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(Application.Context);

            if (queryResult == ConnectionResult.Success)
            {
                if(mGoogleApiClient==null)
                {
                    mGoogleApiClient = new GoogleApiClient.Builder(Application.Context).AddApi(LocationServices.API).AddConnectionCallbacks(this).AddOnConnectionFailedListener(this).Build();
                    string message = string.Format("{0} - {1}", CrossGeofence.Id, "Google Play services is available.");
                    Debug.WriteLine(message);
                }

                if (!mGoogleApiClient.IsConnected)
                {
                    mGoogleApiClient.Connect();

                }

            }
            else
            {
            
                string message = string.Format("{0} - {1}", CrossGeofence.Id, "Google Play services is unavailable.");
                Debug.WriteLine(message);
                CrossGeofence.GeofenceListener.OnError(message);
            }
        }

        /// <summary>
        /// Google play connection failed handling
        /// </summary>
        /// <param name="result"></param>
        public void OnConnectionFailed(ConnectionResult result)
        {
            int errorCode = result.ErrorCode;
            string message = string.Format("{0} - {1} {2}", CrossGeofence.Id, "Connection to Google Play services failed with error code ", errorCode);
            Debug.WriteLine(message);
            CrossGeofence.GeofenceListener.OnError(message);
      
        }
        internal void SetLastKnownLocation(Location location)
        {
             if(location!=null)
            {
                if(lastKnownGeofenceLocation==null)
                {
                    lastKnownGeofenceLocation = new GeofenceLocation();
                }
                lastKnownGeofenceLocation.Latitude = location.Latitude;
                lastKnownGeofenceLocation.Longitude = location.Longitude;
                double seconds = location.Time / 1000;
                lastKnownGeofenceLocation.Date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local).AddSeconds(seconds);
               
            }

        }
        /// <summary>
        /// On Google play services Connection handling
        /// </summary>
        /// <param name="connectionHint"></param>
 
        public void OnConnected(Bundle connectionHint)
        {

            Location location=LocationServices.FusedLocationApi.GetLastLocation(mGoogleApiClient);
            SetLastKnownLocation(location);

            if (CurrentRequestType == RequestType.Add)
            {
                AddGeofences();
                StartLocationUpdates();
          
            }else if(CurrentRequestType == RequestType.Clear)
            {
                RemoveGeofences();

            }
            else if (CurrentRequestType == RequestType.Delete)
            {
               if(mRequestedRegionIdentifiers != null)
               {
                   RemoveGeofences(mRequestedRegionIdentifiers);
               }
            }

            CurrentRequestType = RequestType.Default;
          
            

        }
        /// <summary>
        /// On Geofence Request Result
        /// </summary>
        /// <param name="result"></param>
        public void OnResult(Object result)
        {
            var res = result.JavaCast<IResult>();
          
            int statusCode = res.Status.StatusCode;
            string message = string.Empty;

            switch (res.Status.StatusCode)
            {
                case GeofenceStatusCodes.SuccessCache:
                case GeofenceStatusCodes.Success:
                    if(CurrentRequestType==RequestType.Add)
                    {
                        message = string.Format("{0} - {1}", CrossGeofence.Id, "Successfully added Geofence.");
                        
                        foreach(GeofenceCircularRegion region in Regions.Values)
                        {
                            CrossGeofence.GeofenceListener.OnMonitoringStarted(region.Id);
                        }
                    }
                    else
                    {
                        message = string.Format("{0} - {1}", CrossGeofence.Id, "Geofence Update Received");
                    }
                    
                    break;
                case GeofenceStatusCodes.Error:
                    message = string.Format("{0} - {1}", CrossGeofence.Id, "Error adding Geofence.");
                    break;
                case GeofenceStatusCodes.GeofenceTooManyGeofences:
                    message = string.Format("{0} - {1}", CrossGeofence.Id, "Too many geofences.");
                    break;
                case GeofenceStatusCodes.GeofenceTooManyPendingIntents:
                    message = string.Format("{0} - {1}", CrossGeofence.Id, "Too many pending intents.");
                    break;
                case GeofenceStatusCodes.GeofenceNotAvailable:
                    message = string.Format("{0} - {1}", CrossGeofence.Id, "Geofence not available.");
                    break;
            }

            Debug.WriteLine(message);

            if (statusCode != GeofenceStatusCodes.Success && statusCode != GeofenceStatusCodes.SuccessCache && IsMonitoring)
            {
                StopMonitoringAllRegions();


                if (!string.IsNullOrEmpty(message))
                    CrossGeofence.GeofenceListener.OnError(message);
            }
        }
       /// <summary>
       /// Connection suspended handling
       /// </summary>
       /// <param name="cause"></param>
       public void OnConnectionSuspended(int cause)
       {
            string message = string.Format("{0} - {1} {2}", CrossGeofence.Id, "Connection to Google Play services suspended with error code ", cause);
            Debug.WriteLine(message);
            CrossGeofence.GeofenceListener.OnError(message);
      }

      private void RemoveRegion(string regionIdentifier)
      {
          if (mRegions.ContainsKey(regionIdentifier))
          {
              mRegions.Remove(regionIdentifier);
          }

          if (mGeofenceResults.ContainsKey(regionIdentifier))
          {
              mGeofenceResults.Remove(regionIdentifier);
          }
      }

      internal void StartLocationUpdates()
      {
          LocationRequest mLocationRequest = new LocationRequest();
          mLocationRequest.SetInterval(CrossGeofence.LocationUpdatesInterval == 0 ? 30000 : CrossGeofence.LocationUpdatesInterval);
          mLocationRequest.SetFastestInterval(CrossGeofence.FastestLocationUpdatesInterval == 0 ? 5000 : CrossGeofence.FastestLocationUpdatesInterval);
          string priorityType="Balanced Power";
          switch(CrossGeofence.GeofencePriority)
          {
              case GeofencePriority.HighAccuracy:
                  priorityType="High Accuracy";
                  mLocationRequest.SetPriority(LocationRequest.PriorityHighAccuracy);
                  break;
              case GeofencePriority.LowAccuracy:
                  priorityType="Low Accuracy";
                  mLocationRequest.SetPriority(LocationRequest.PriorityLowPower);
                  break;
              case GeofencePriority.LowestAccuracy:
                  priorityType="Lowest Accuracy";
                  mLocationRequest.SetPriority(LocationRequest.PriorityNoPower);
                  break;
              case GeofencePriority.MediumAccuracy:
              case GeofencePriority.AcceptableAccuracy:
              default:
                  mLocationRequest.SetPriority(LocationRequest.PriorityBalancedPowerAccuracy);
                  break;
          }
        
          Debug.WriteLine("{0} - {1}: {2}", CrossGeofence.Id, "Priority set to", priorityType);
          //(Regions.Count == 0) ? (CrossGeofence.SmallestDisplacement==0?50 :CrossGeofence.SmallestDisplacement): Regions.Min(s => (float)s.Value.Radius)
          if(CrossGeofence.SmallestDisplacement>0)
          {
              mLocationRequest.SetSmallestDisplacement(CrossGeofence.SmallestDisplacement);
              Debug.WriteLine("{0} - {1}: {2} meters", CrossGeofence.Id, "Location smallest displacement set to", CrossGeofence.SmallestDisplacement);
          }
     
          LocationServices.FusedLocationApi.RequestLocationUpdates(mGoogleApiClient, mLocationRequest, GeofenceLocationListener.SharedInstance);
      }
      internal void StopLocationUpdates()
      {
          LocationServices.FusedLocationApi.RemoveLocationUpdates(mGoogleApiClient, GeofenceLocationListener.SharedInstance);
      }

     
    }
}
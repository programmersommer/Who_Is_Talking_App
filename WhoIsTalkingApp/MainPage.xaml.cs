using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


using Microsoft.ProjectOxford.SpeakerRecognition;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification;
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using System.Threading.Tasks;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WhoIsTalkingApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {


        private SpeakerIdentificationServiceClient _serviceClient;
        private string _subscriptionKey;

        DispatcherTimer _etimer;
        DispatcherTimer _itimer;

        private MediaCapture CaptureMedia;
        private IRandomAccessStream AudioStream;

        public MainPage()
        {
            this.InitializeComponent();

            CaptureMedia = null;

            _etimer = new DispatcherTimer();
            _etimer.Interval = new TimeSpan(0, 0, 60);
            _etimer.Tick += EnrollmentTime_Over;

            _itimer = new DispatcherTimer();
            _itimer.Interval = new TimeSpan(0, 0, 60);
            _itimer.Tick += IdentificationTime_Over;

            _subscriptionKey = "put_your_subscription_key_here";
            _serviceClient = new SpeakerIdentificationServiceClient(_subscriptionKey);

        }


        private async void btnRecordEnroll_Click(object sender, RoutedEventArgs e)
        {

            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles and select one of them";
                return;
            }


            if (CaptureMedia == null)
            {
                btnRecordEnroll.Content = "Stop record enrollment";
                btnIdentify.IsEnabled = false;

                CaptureMedia = new MediaCapture();
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                await CaptureMedia.InitializeAsync(captureInitSettings);
                MediaEncodingProfile encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                encodingProfile.Audio.ChannelCount = 1;
                encodingProfile.Audio.SampleRate = 16000;
                AudioStream = new InMemoryRandomAccessStream();
                CaptureMedia.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
                CaptureMedia.Failed += MediaCaptureOnFailed;
                await CaptureMedia.StartRecordToStreamAsync(encodingProfile, AudioStream);

                _etimer.Start();
            }
            else
            {
                _etimer.Stop();
                await FinishEnrollment();
            }
        }


        async Task FinishEnrollment()
        {

            btnRecordEnroll.Content = "Start record enrollment";
            btnRecordEnroll.IsEnabled = false;
            await CaptureMedia.StopRecordAsync();

            Stream str = AudioStream.AsStream();
            str.Seek(0, SeekOrigin.Begin);


            Guid _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            OperationLocation processPollingLocation;
            try
            {
                processPollingLocation = await _serviceClient.EnrollAsync(str, _speakerId);
            }
            catch (EnrollmentException vx)
            {
                txtInfo.Text = vx.Message;
                CaptureMedia = null;
                btnRecordEnroll.IsEnabled = true;
                btnIdentify.IsEnabled = true;
                return;
            }


            EnrollmentOperation enrollmentResult = null;
            int numOfRetries = 10;
            TimeSpan timeBetweenRetries = TimeSpan.FromSeconds(5.0);
            while (numOfRetries > 0)
            {
                await Task.Delay(timeBetweenRetries);
                enrollmentResult = await _serviceClient.CheckEnrollmentStatusAsync(processPollingLocation);

                if (enrollmentResult.Status == Status.Succeeded)
                {
                    break;
                }
                else if (enrollmentResult.Status == Status.Failed)
                {
                    txtInfo.Text = enrollmentResult.Message;
                    CaptureMedia = null;
                    btnRecordEnroll.IsEnabled = true;
                    btnIdentify.IsEnabled = true;
                    return;
                }
                numOfRetries--;
            }

            if (numOfRetries <= 0)
            {
                txtInfo.Text = "Identification operation timeout";
            }
            else
            {
                txtInfo.Text = "Enrollment done. " + enrollmentResult.Status + Environment.NewLine + " Remaining Speech Time " + enrollmentResult.ProcessingResult.RemainingEnrollmentSpeechTime;
            }

            CaptureMedia = null;
            btnRecordEnroll.IsEnabled = true;
            btnIdentify.IsEnabled = true;
        }


        private async void EnrollmentTime_Over(object sender, object e)
        {
            _etimer.Stop();

            await FinishEnrollment();
        }

        private async void IdentificationTime_Over(object sender, object e)
        {
            _itimer.Stop();
            await finishIdentification();
        }

        private void MediaCaptureOnRecordLimitationExceeded(MediaCapture sender)
        {
            throw new NotImplementedException();
        }

        private void MediaCaptureOnFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            throw new NotImplementedException();
        }

        private async void btnIdentify_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.Items.Count < 1)
            {
                txtInfo.Text = "Get profiles";
                return;
            }

            if (CaptureMedia == null)
            {
                btnIdentify.Content = "Stop voice identification";
                btnRecordEnroll.IsEnabled = false;

                CaptureMedia = new MediaCapture();
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                await CaptureMedia.InitializeAsync(captureInitSettings);
                MediaEncodingProfile encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                encodingProfile.Audio.ChannelCount = 1;
                encodingProfile.Audio.SampleRate = 16000;
                AudioStream = new InMemoryRandomAccessStream();
                CaptureMedia.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
                CaptureMedia.Failed += MediaCaptureOnFailed;
                await CaptureMedia.StartRecordToStreamAsync(encodingProfile, AudioStream);

                _itimer.Start();
            }
            else
            {
                _itimer.Stop();
                await finishIdentification();
            }

        }

        async Task finishIdentification()
        {
            btnIdentify.Content = "Start voice identification";
            btnIdentify.IsEnabled = false;
            await CaptureMedia.StopRecordAsync();

            Stream str = AudioStream.AsStream();
            str.Seek(0, SeekOrigin.Begin);

            Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification.Profile[] selectedProfiles = new Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification.Profile[lbProfiles.Items.Count]; ;


            Guid[] testProfileIds = new Guid[selectedProfiles.Length];
            for (int i = 0; i < lbProfiles.Items.Count; i++)
            {
                testProfileIds[i] = Guid.Parse((lbProfiles.Items[i] as ListBoxItem).Content.ToString());
            }


            OperationLocation processPollingLocation;
            try
            {
                processPollingLocation = await _serviceClient.IdentifyAsync(str, testProfileIds);
            }
            catch (IdentificationException vx)
            {
                txtInfo.Text = vx.Message;
                CaptureMedia = null;
                btnRecordEnroll.IsEnabled = true;
                btnIdentify.IsEnabled = true;
                return;
            }

            IdentificationOperation identificationResponse = null;
            int numOfRetries = 10;
            TimeSpan timeBetweenRetries = TimeSpan.FromSeconds(5.0);
            while (numOfRetries > 0)
            {
                await Task.Delay(timeBetweenRetries);
                identificationResponse = await _serviceClient.CheckIdentificationStatusAsync(processPollingLocation);

                if (identificationResponse.Status == Status.Succeeded)
                {
                    break;
                }
                else if (identificationResponse.Status == Status.Failed)
                {
                    txtInfo.Text = identificationResponse.Message;
                    CaptureMedia = null;
                    btnRecordEnroll.IsEnabled = true;
                    btnIdentify.IsEnabled = true;
                    return;
                }
                numOfRetries--;
            }

            if (numOfRetries <= 0)
            {
                txtInfo.Text = "Identification operation timeout.";
            }
            else
            { 
            txtInfo.Text = identificationResponse.ProcessingResult.IdentifiedProfileId.ToString();
            txtInfo.Text = txtInfo.Text + Environment.NewLine + identificationResponse.ProcessingResult.Confidence.ToString();
            }

            CaptureMedia = null;
            btnRecordEnroll.IsEnabled = true;
            btnIdentify.IsEnabled = true;
        }


        private async void btnGetProfiles_Click(object sender, RoutedEventArgs e)
        {
            await GetProfiles();
        }

        async Task GetProfiles()
        {

            try // I don't want to disable buttons and for multiple clicks error catching
            {
                Profile[] profiles = await _serviceClient.GetProfilesAsync();

                lbProfiles.Items.Clear();
                foreach (Profile _profile in profiles)
                {
                    ListBoxItem lbi = new ListBoxItem();
                    lbi.Content = _profile.ProfileId;
                    lbProfiles.Items.Add(lbi);
                }
            }
            catch { }
        }


        private async void btnResetEnroll_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles";
                return;
            }

            Guid _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            try
            {
                await _serviceClient.ResetEnrollmentsAsync(_speakerId);
                txtInfo.Text = "Enrollments reset operation succesful";
            }
            catch { }
        }

        private async void btnCreateProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateProfileResponse response = await _serviceClient.CreateProfileAsync("en-us");
                txtInfo.Text = "Profile Created: " + response.ProfileId;
            }
            catch { }

            await GetProfiles();
        }


        private async void btnRemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles";
                return;
            }

            Guid _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            try
            {
                await _serviceClient.DeleteProfileAsync(_speakerId);
                txtInfo.Text = "Profile deleted";
            }
            catch { }

            await GetProfiles();
        }

    }
}

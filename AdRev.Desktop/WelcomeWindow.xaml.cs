using System.Windows;
using System.Text.RegularExpressions;
using AdRev.Core.Services;
using AdRev.Domain.Models;
using AdRev.Desktop.Services;

namespace AdRev.Desktop
{
    public partial class WelcomeWindow : Window
    {
        public UserProfile? Profile { get; private set; }

        public WelcomeWindow()
        {
            InitializeComponent();
            var service = new LicensingService();
            HwidBox.Text = service.GetHardwareId();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CopyHwid_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(EmailBox.Text))
            {
                MessageBox.Show("Veuillez remplir votre Nom et Email avant de copier la demande.", "Infos manquantes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string type = (LicenseTypeBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Standard";
            string orderRequest = $"DESTINATAIRE : baigho2@gmail.com\nCOMMANDE ADREV\n------------------\nNOM : {NameBox.Text} {TitleBox.Text}\nEMAIL : {EmailBox.Text}\nTYPE : {type}\nHWID : {HwidBox.Text}\nPAIEMENT : [Joindre Capture/Facture]\n------------------";
            
            Clipboard.SetText(orderRequest);
            MessageBox.Show($"Les informations de commande ont été copiées !\n\nEnvoyez ce message à : baigho2@gmail.com\nAccompagné de votre preuve de paiement pour obtenir votre clé.", "Demande Copiée", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            string key = LicenseBox.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Veuillez entrer une clé de licence pour activer.", "Clé manquante", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var service = new LicensingService();
            if (service.Activate(key))
            {
                SaveProfile();
                MessageBox.Show("Félicitations ! Votre licence AdRev Pro est activée.", "Activation Réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Clé de licence invalide ou expirée.", "Erreur d'Activation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Trial_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            var service = new LicensingService();
            service.StartTrial();

            SaveProfile();
            MessageBox.Show("Votre période d'essai de 7 jours commence maintenant !", "Essai Activé", MessageBoxButton.OK, MessageBoxImage.Information);
            
            this.DialogResult = true;
            this.Close();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(EmailBox.Text))
            {
                MessageBox.Show("Veuillez entrer votre nom et votre email pour continuer.", "Information manquante", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (!Regex.IsMatch(EmailBox.Text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                 MessageBox.Show("Veuillez entrer une adresse email valide.", "Email Invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return false;
            }
            return true;
        }

        private void SaveProfile()
        {
            string title = (TitleBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
            
            var profileService = new ResearcherProfileService();
            var profile = profileService.GetProfile();
            profile.Title = title;
            profile.FullName = NameBox.Text;
            profile.Institution = ""; // Can be added later
            profileService.SaveProfile(profile);

            Profile = new UserProfile
            {
                Title = title,
                LastName = NameBox.Text,
                Email = EmailBox.Text
            };
        }
    }
}

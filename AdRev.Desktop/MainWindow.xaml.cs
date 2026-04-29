using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using AdRev.Core.Services;
using AdRev.Core.Common;
using AdRev.Domain.Models;
using AdRev.Desktop.Windows;

namespace AdRev.Desktop
{
    public class ProjectViewModel
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? LastModified { get; set; }
        public ResearchProject? Project { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly ResearchProjectService _projectService = new ResearchProjectService();
        private readonly LicensingService _licensingService = new LicensingService();
        private readonly FeatureManager _featureManager;
        private readonly ResearcherProfileService _profileService = new ResearcherProfileService();

        public ObservableCollection<ProjectViewModel> RecentProjects { get; set; } = new ObservableCollection<ProjectViewModel>();
        private ResearchProject? _currentProject;
        private List<Window> _openProjectWindows = new List<Window>();

        public MainWindow()
        {
            _featureManager = new FeatureManager(_licensingService);
            InitializeComponent();
            DataContext = this;
            
            LoadUserProfile();
            LoadProjects();
            UpdateLicenseDisplay();
            
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckLicense();
        }

        private void CheckLicense()
        {
            if (!_licensingService.IsActivated(out string message))
            {
                var welcome = new WelcomeWindow();
                if (welcome.ShowDialog() == true)
                {
                    LoadUserProfile();
                    UpdateLicenseDisplay();
                }
                else if (!_licensingService.IsActivated(out _))
                {
                    MessageBox.Show("Une licence valide est requise pour utiliser AdRev.", "Licence Requise", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Application.Current.Shutdown();
                }
            }
        }

        private void LoadUserProfile()
        {
            var profile = _profileService.GetProfile();
            UserProfileName.Text = profile.FullName;
            UserProfileTitle.Text = profile.Title;
            
            int hour = DateTime.Now.Hour;
            string greeting = (hour >= 18 || hour < 5) ? "Bonsoir" : "Bonjour";
            DashboardTitle.Text = $"👋 {greeting}, {profile.FullName}";
        }

        private void LoadProjects()
        {
            var projects = _projectService.GetAllProjects().OrderByDescending(p => p.CreatedOn).ToList();
            RecentProjects.Clear();
            foreach (var p in projects.Take(10))
            {
                RecentProjects.Add(new ProjectViewModel
                {
                    Name = p.Title,
                    Type = p.StudyType.ToString(),
                    Status = p.Status.ToString(),
                    LastModified = p.CreatedOn.ToString("g"),
                    Project = p
                });
            }

            StatOngoing.Text = projects.Count(p => p.Status == ProjectStatus.Ongoing).ToString();
            StatValidated.Text = projects.Count(p => p.Status == ProjectStatus.Completed).ToString();
        }

        private void UpdateLicenseDisplay()
        {
            _licensingService.IsActivated(out string status);
            LicenseStatusText.Text = status;
            
            var license = _licensingService.GetCurrentLicense();
            BtnUpgrade.Visibility = (license != null && (license.Type == LicenseType.Student || license.Type == LicenseType.Pro)) 
                                    ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SwitchView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                DashboardContent.Visibility = Visibility.Collapsed;
                ActiveViewScroll.Visibility = Visibility.Collapsed;
                PageTitle.Text = tag;

                UpdateActiveMenu(btn);

                switch (tag)
                {
                    case "Dashboard":
                        DashboardContent.Visibility = Visibility.Visible;
                        PageTitle.Text = "Tableau de bord";
                        break;
                    case "Projects":
                        ActiveViewContent.Content = new AdRev.Desktop.Views.Project.ProjectListView();
                        ActiveViewScroll.Visibility = Visibility.Visible;
                        break;
                    case "Quality":
                        var qualityView = new AdRev.Desktop.Views.Project.QualityCheckView();
                        qualityView.LoadProject(new ResearchProject { Title = "Analyse Qualité Rapide" });
                        ActiveViewContent.Content = qualityView;
                        ActiveViewScroll.Visibility = Visibility.Visible;
                        PageTitle.Text = "Contrôle Qualité";
                        break;
                    case "Analysis":
                        var intro = new QuickAnalysisIntroWindow();
                        intro.Owner = this;
                        if (intro.ShowDialog() == true)
                        {
                            var analysisView = new AdRev.Desktop.Views.Project.AnalysisView();
                            var quickProject = new ResearchProject 
                            { 
                                Title = intro.ProjectTitle, 
                                Authors = intro.AuthorName,
                                CreatedOn = DateTime.Now,
                                Status = ProjectStatus.Ongoing
                            };
                            analysisView.LoadProject(quickProject);
                            ActiveViewContent.Content = analysisView;
                            ActiveViewScroll.Visibility = Visibility.Visible;
                            PageTitle.Text = "Analyse Rapide : " + intro.ProjectTitle;
                        }
                        else
                        {
                            DashboardContent.Visibility = Visibility.Visible;
                            PageTitle.Text = "Tableau de bord";
                        }
                        break;
                    case "ExportFolder":
                        ActiveViewContent.Content = new AdRev.Desktop.Views.Project.LibraryView(); // Using LibraryView as a folder management view
                        ActiveViewScroll.Visibility = Visibility.Visible;
                        break;
                    case "MobileSync":
                        var syncWindow = new MobileSyncWindow();
                        syncWindow.Owner = this;
                        syncWindow.ShowDialog();
                        
                        DashboardContent.Visibility = Visibility.Visible;
                        PageTitle.Text = "Tableau de bord";
                        break;
                    default:
                        DashboardContent.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void UpdateActiveMenu(Button activeBtn)
        {
            var buttons = new List<Button> { BtnDashboard, BtnProjects, BtnAnalysis, BtnQuality, BtnExportFolder, BtnMobileSync };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.Background = System.Windows.Media.Brushes.Transparent;
                btn.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"));
            }

            if (activeBtn != null)
            {
                activeBtn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EEF2FF"));
                activeBtn.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1"));
            }
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var win = new NewProjectWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                LoadProjects();
                OpenProjectWindow(win.CreatedProject);
            }
        }

        private void ImportProjectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var project = _projectService.ImportFromFolder(dialog.FolderName);
                    LoadProjects();
                    MessageBox.Show($"Projet '{project.Title}' importé.", "Importation réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur: {ex.Message}", "Erreur d'importation", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            ResearchProject? project = null;
            if (sender is Button btn && btn.DataContext is ProjectViewModel vm)
            {
                project = vm.Project;
            }

            if (project != null)
            {
                OpenProjectWindow(project);
            }
        }

        private void OpenProjectWindow(ResearchProject project)
        {
            var existing = _openProjectWindows.OfType<ProjectWindow>().FirstOrDefault(w => w.Project.Id == project.Id);
            if (existing != null)
            {
                existing.Activate();
                return;
            }

            var win = new ProjectWindow(project);
            win.Closed += (s, e) => _openProjectWindows.Remove(win);
            _openProjectWindows.Add(win);
            win.Show();
        }

        private void EditProfile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = new ProfileWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                LoadUserProfile();
            }
        }

        private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string culture)
            {
                ((App)Application.Current).SetLanguage(culture);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenUpgrade_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Visitez adrev-science.com", "Mise à niveau", MessageBoxButton.OK, MessageBoxImage.Information);
        private void OpenActivation_Click(object sender, RoutedEventArgs e)
        {
            var activation = new ActivationWindow();
            if (activation.ShowDialog() == true)
            {
                UpdateLicenseDisplay();
            }
        }
    }
}

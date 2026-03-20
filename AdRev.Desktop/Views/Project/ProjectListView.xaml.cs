using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AdRev.Desktop;

namespace AdRev.Desktop.Views.Project
{
    public partial class ProjectListView : UserControl
    {
        private readonly AdRev.Core.Common.ResearchProjectService _projectService = new AdRev.Core.Common.ResearchProjectService();
        public ObservableCollection<ProjectViewModel> RecentProjects { get; set; } = new ObservableCollection<ProjectViewModel>();
        public ObservableCollection<ProjectViewModel> AllProjects { get; set; } = new ObservableCollection<ProjectViewModel>();

        public ProjectListView()
        {
            InitializeComponent();
            InitializeData();
            
            // Binding
            RecentProjectsControl.ItemsSource = RecentProjects;
            AllProjectsGrid.ItemsSource = AllProjects;
        }

        private void InitializeData()
        {
            // The user requested to clear all test/seed projects from the list.
            RecentProjects.Clear();
            AllProjects.Clear();

            // Load real projects from disk instead of hardcoded seeds
            var projects = _projectService.GetAllProjects().OrderByDescending(p => p.CreatedOn).ToList();
            
            foreach (var p in projects.Take(7))
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

            foreach (var p in projects)
            {
                AllProjects.Add(new ProjectViewModel
                {
                    Name = p.Title,
                    Type = p.StudyType.ToString(),
                    Status = p.Status.ToString(),
                    LastModified = p.CreatedOn.ToString("g"),
                    Project = p
                });
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ProjectViewModel vm)
            {
                 MessageBox.Show($"Ouverture du projet : {vm.Name}", "Ouverture");
                 // Logic to actually open would go here
            }
        }
    }

    // Reuse or redefine if not accessible
    // public class ProjectViewModel { ... } is already in MainWindow namespace ?
    // Assuming partial class structure or using the one from MainWindow if public.
    // Ideally should be in a separate file. For now, let's redefine narrowly or rely on using namespace.
}

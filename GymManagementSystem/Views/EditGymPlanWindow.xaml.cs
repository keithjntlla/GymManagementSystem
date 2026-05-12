using GymManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GymManagementSystem.Views
{
    public partial class EditGymPlanWindow : Window
    {
        public GymPlan EditedPlan { get; private set; }

        public EditGymPlanWindow(GymPlan plan)
        {
            InitializeComponent();
            EditedPlan = plan;

            // Load existing data
            txtPlanName.Text = plan.PlanName;
            txtPrice.Text = plan.Price.ToString();
            txtDuration.Text = plan.DurationDays.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. First validate that the input values are in the correct format
            if (double.TryParse(txtPrice.Text, out double price) &&
                int.TryParse(txtDuration.Text, out int duration))
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to save changes to '{txtPlanName.Text}'?\n",
                    "Confirm Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // 3. Only proceed if the user clicks 'Yes'
                if (result == MessageBoxResult.Yes)
                {
                    EditedPlan.PlanName = txtPlanName.Text;
                    EditedPlan.Price = price;
                    EditedPlan.DurationDays = duration;

                    this.DialogResult = true;
                    this.Close();
                }
                // If they click 'No', the window stays open so they can keep editing or cancel manually
            }
            else
            {
                MessageBox.Show("Please enter valid numbers for Price and Duration.",
                                "Validation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}
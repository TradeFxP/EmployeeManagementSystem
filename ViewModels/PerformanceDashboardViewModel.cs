using System;
using System.Collections.Generic;

namespace UserRoles.Models.ViewModels
{
    public class PerformanceDashboardViewModel
    {
        public DashboardKPIs KPIs { get; set; } = new();
        public List<TrendPoint> TrendData { get; set; } = new();
        public List<EmployeeRatingRow> EmployeeRows { get; set; } = new();
        public List<TeamAvgRating> TeamAvgRatings { get; set; } = new();
        public RatingDistribution Distribution { get; set; } = new();
        public FilterOptions Filters { get; set; } = new();

        // Active Filters
        public string SelectedTeam { get; set; } = "All Teams";
        public string SelectedRating { get; set; } = "All Ratings";
        public string SelectedPeriod { get; set; } = "Monthly View";
        public string SearchQuery { get; set; }

        // Date Display
        public string CurrentDateRange { get; set; } = "Feb 1 - Feb 28, 2026";
        public string TodayDateFormatted { get; set; } = DateTime.Now.ToString("MMM d, yyyy");

        // Review Modal Data
        public AddReviewViewModel AddReview { get; set; } = new();
        public List<EmployeeLookupItem> AvailableEmployees { get; set; } = new();
    }

    public class DashboardKPIs
    {
        public int TotalEmployees { get; set; }
        public double TotalEmployeesChange { get; set; }
        public decimal AvgRating { get; set; }
        public double AvgRatingChange { get; set; }
        public int TopPerformers { get; set; }
        public double TopPerformersChange { get; set; }
        public int NeedsImprovement { get; set; }
        public double NeedsImprovementChange { get; set; }
    }

    public class TrendPoint
    {
        public string Month { get; set; }
        public Dictionary<string, decimal> TeamScores { get; set; } = new();
    }

    public class EmployeeRatingRow
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Team { get; set; }
        public decimal Rating { get; set; }
        public string TasksDone { get; set; }
        public int TasksPct { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public string Color { get; set; }
        public string Initials { get; set; }
        public string AvatarColor { get; set; }
        public List<PerformanceMetric> Metrics { get; set; } = new();
    }

    public class PerformanceMetric
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public string Color { get; set; }
    }

    public class TeamAvgRating
    {
        public string TeamName { get; set; }
        public decimal AvgRating { get; set; }
        public string Color { get; set; }
    }

    public class RatingDistribution
    {
        public int ExcellentPct { get; set; }
        public int GoodPct { get; set; }
        public int AveragePct { get; set; }
        public int PoorPct { get; set; }
    }

    public class FilterOptions
    {
        public List<string> Teams { get; set; } = new();
        public List<string> Ratings { get; set; } = new() { "Excellent", "Good", "Average", "Poor" };
        public List<string> Periods { get; set; } = new() { "Weekly View", "Monthly View", "Quarterly View" };
    }

    public class AddReviewViewModel
    {
        public string EmployeeId { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
        public int TasksCompleted { get; set; }
        public decimal Rating { get; set; }
        public string Notes { get; set; }
    }

    public class EmployeeLookupItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
    }
}

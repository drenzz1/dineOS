namespace DineOS.Application.DTOs;

public record WeeklyGrowthDto(string Week, int NewRestaurants);

public record TopRestaurantDto(
    int Rank,
    string Name,
    int Orders,
    decimal Revenue);

public record ActivityEventDto(
    string Id,
    string Description,
    DateTime Timestamp);

public record AdminAnalyticsDto(
    int TotalRestaurants,
    int ActiveRestaurants,
    int OrdersToday,
    decimal RevenueToday,
    List<WeeklyGrowthDto> WeeklyGrowth,
    List<TopRestaurantDto> TopRestaurants,
    List<ActivityEventDto> ActivityFeed);

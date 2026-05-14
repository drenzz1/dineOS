namespace DineOS.Application.DTOs;

public record RestaurantDto(
    long Id,
    string Name,
    string OwnerName,
    string OwnerEmail,
    string Phone,
    string City,
    string Plan,
    string Status,
    int TotalOrders,
    int StaffCount,
    decimal Revenue,
    DateTime CreatedAt,
    bool OwnerEmailVerified
);

namespace DineOS.Application.DTOs;

public record RestaurantProfileDto(
    long Id,
    string Name,
    string Slug,
    string OwnerName,
    string OwnerEmail,
    string Phone,
    string City,
    string Plan,
    string Status,
    DateTime CreatedAt);

﻿namespace Store.Service.Services.BasketService.Dtos
{
public class CustomerBasketDto
{
    public string? Id { get; set; }
    public int? DeliveryMethodId { get; set; }
    public decimal ShippingPrice { get; set; }
    public List<BasketItemDto> BasketItems { get; set; } = new List<BasketItemDto>();
}
}

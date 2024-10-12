﻿using AutoMapper;
using Microsoft.Extensions.Configuration;
using Store.Data.Entites;
using Store.Data.Entities.OrderEntities;
using Store.Data.Entities;
using Store.Repository.Interfaces;
using Store.Service.Services.BasketService.Dtos;
using Store.Service.Services.BasketService;
using Store.Service.Services.OrderService.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stripe;
using Product = Store.Data.Entites.Product;
using Store.Repository.Specefication.OrderSpecs;

namespace Store.Service.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBasketService _basketService;
        private readonly IMapper _mapper;

        public PaymentService(IConfiguration configuration, IUnitOfWork unitOfWork, IBasketService basketService, IMapper mapper)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _basketService = basketService;
            _mapper = mapper;
        }
        public async Task<CustomerBasketDto> CreateOrUpdatePaymentIntent(CustomerBasketDto basket)
        {
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            if (basket is null)
                throw new Exception("Basket is Empty");

            var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod, int>().GetByIdAsync(basket.DeliveryMethodId.Value);

            if (deliveryMethod is null)
                throw new Exception("Delivery Method Not Provided");

            decimal shippingPrice = deliveryMethod.Price;

            foreach (var item in basket.BasketItems)
            {
                var product = await _unitOfWork.Repository<Product, int>().GetByIdAsync(item.ProductId);
                if (item.Price != product.Price)
                    item.Price = product.Price;
            }

            var service = new PaymentIntentService();
            PaymentIntent paymentIntent;

            if (string.IsNullOrEmpty(basket.PaymentIntentId))
            {
                var options = new PaymentIntentCreateOptions()
                {
                    Amount = (long)basket.BasketItems.Sum(x => x.Quantity * (x.Price * 100)) + (long)shippingPrice * 100,
                    Currency = "usd",
                    PaymentMethodTypes = new List<string> { "card" }
                };
                paymentIntent = await service.CreateAsync(options);

                basket.PaymentIntentId = paymentIntent.Id;
                basket.ClientSecret = paymentIntent.ClientSecret;
            }
            else
            {
                var options = new PaymentIntentUpdateOptions
                {
                    Amount = (long)basket.BasketItems.Sum(x => x.Quantity * (x.Price * 100)) + (long)shippingPrice * 100
                };
                await service.UpdateAsync(basket.PaymentIntentId, options);
            }

            await _basketService.UpdateBasketAsync(basket);

            return basket;
        }

        public async Task<OrderDetailsDto> UpdateOrderPaymentFailed(string paymentIntentId)
        {
            var spec = new OrderWithPaymentIntentSpecefication(paymentIntentId);
            var order = await _unitOfWork.Repository<Order, Guid>().GetBySpecificatiobIdAsync(spec);

            if (order is null)
                throw new Exception("Order Does Not Exist");

            order.OrderPaymentStatus = OrderPaymentStatus.Failed;
            _unitOfWork.Repository<Order, Guid>().Update(order);
            await _unitOfWork.CompleteAsync();
            var mappedOrder = _mapper.Map<OrderDetailsDto>(order);
            return mappedOrder;
        }

        public async Task<OrderDetailsDto> UpdateOrderPaymentSucceeded(string paymentIntentId)
        {
            var spec = new OrderWithPaymentIntentSpecefication(paymentIntentId);
            var order = await _unitOfWork.Repository<Order, Guid>().GetBySpecificatiobIdAsync(spec);

            if (order is null)
                throw new Exception("Order Does Not Exist");

            order.OrderPaymentStatus = OrderPaymentStatus.Received;

            _unitOfWork.Repository<Order, Guid>().Update(order);

            await _unitOfWork.CompleteAsync();

            await _basketService.DeleteBasketAsync(order.BasketId);

            var mappedOrder = _mapper.Map<OrderDetailsDto>(order);

            return mappedOrder;
        }
    }
}

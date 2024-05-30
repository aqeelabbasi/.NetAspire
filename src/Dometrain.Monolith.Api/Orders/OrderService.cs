using System.Diagnostics.Metrics;
using Dometrain.Monolith.Api.Enrollments;
using Dometrain.Monolith.Api.Students;
using MassTransit;

namespace Dometrain.Monolith.Api.Orders;

public interface IOrderService
{
    Task<Order?> GetByIdAsync(Guid orderId);

    Task<IEnumerable<Order>> GetAllForStudentAsync(Guid studentId);

    Task<Order?> PlaceAsync(Guid studentId, IEnumerable<Guid> courseIds);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IBus _bus;
    private readonly Counter<long> _orderCounter;
    
    public OrderService(IOrderRepository orderRepository, IStudentRepository studentRepository, IBus bus, IMeterFactory meterFactory)
    {
        _orderRepository = orderRepository;
        _studentRepository = studentRepository;
        _bus = bus;
        var meter = meterFactory.Create("Dometrain.Api");
        _orderCounter = meter.CreateCounter<long>("dometrain.api.orders.count");
    }

    public async Task<Order?> GetByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<IEnumerable<Order>> GetAllForStudentAsync(Guid studentId)
    {
        return await _orderRepository.GetAllForStudentAsync(studentId);
    }

    public async Task<Order?> PlaceAsync(Guid studentId, IEnumerable<Guid> courseIds)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        _orderCounter.Add(1);
        if (student is null)
        {
            return null;
        }

        var orderIdsList = courseIds.ToList();
        
        var order = await _orderRepository.PlaceAsync(studentId, orderIdsList);

        if (order is null)
        {
            return null;
        }

        var orderPlaced = new OrderPlaced(order.Id, studentId, orderIdsList);
        await _bus.Publish(orderPlaced);
        _orderCounter.Add(1);
        return order;
    }
}

public record OrderPlaced(Guid  OrderId, Guid StudentId, IEnumerable<Guid> CourseIds);

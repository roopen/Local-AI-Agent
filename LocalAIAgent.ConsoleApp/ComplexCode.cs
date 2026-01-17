namespace LocalAIAgent.ConsoleApp
{
    public class OrderProcessor
    {
        public string ProcessOrder(Order order)
        {
            string status = "Processing";
            int totalItems = 0;
            double totalPrice = 0;
            bool hasDiscount = false;
            bool isPriority = false;

            if (order != null)
            {
                if (order.Items != null && order.Items.Count > 0)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        if (item != null)
                        {
                            totalItems++;
                            totalPrice += item.Price * item.Quantity;

                            if (item.IsDiscounted)
                            {
                                hasDiscount = true;
                                if (item.DiscountPercentage > 0)
                                {
                                    totalPrice -= item.Price * item.Quantity * item.DiscountPercentage / 100;
                                }
                            }

                            if (item.IsPriority)
                            {
                                isPriority = true;
                                // Further nesting for complexity
                                if (item.Quantity > 5 && item.Price > 50)
                                {
                                    if (item.ItemName.Contains("Premium") || item.ItemName.Contains("Luxury"))
                                    {
                                        status += " - High Value Premium Item";
                                    }
                                }
                            }
                        }
                        else
                        {
                            status = "Error: Null item found";
                            // Nested error handling
                            if (order.OrderId == Guid.Empty)
                            {
                                status += " - Invalid Order ID";
                                // Even more nesting
                                if (order.Items.Count == 1)
                                {
                                    status += " - Single Item Order";
                                }
                            }
                        }
                    }

                    if (totalItems > 10)
                    {
                        status = "Large Order";
                        if (totalPrice > 1000)
                        {
                            status += " - High Value";
                            if (hasDiscount)
                            {
                                status += " with Discount";
                            }
                            if (isPriority)
                            {
                                status += " and Priority";
                            }
                        }
                        else if (totalPrice > 500)
                        {
                            status += " - Medium Value";
                        }
                    }
                    else if (totalItems > 5)
                    {
                        status = "Medium Order";
                        if (totalPrice > 500)
                        {
                            status += " - High Value";
                        }
                    }
                    else
                    {
                        status = "Small Order";
                    }

                    // More nested logic
                    if (order.ShippingAddress != null)
                    {
                        if (order.ShippingAddress.Country == "USA")
                        {
                            if (order.ShippingAddress.State == "CA")
                            {
                                status += " - California Shipping";
                            }
                            else if (order.ShippingAddress.State == "NY")
                            {
                                status += " - New York Shipping";
                            }
                        }
                        else if (order.ShippingAddress.Country == "Canada")
                        {
                            status += " - Canadian Shipping";
                        }
                    }

                    // Another loop for complexity
                    for (int i = 0; i < totalItems; i++)
                    {
                        if (i % 2 == 0)
                        {
                            // Do something trivial to add complexity
                            int temp = i * 2;
                        }
                        else
                        {
                            int temp = i / 2;
                        }
                    }

                    // Deeply nested condition
                    if (totalPrice > 2000 && totalItems > 15 && hasDiscount && isPriority)
                    {
                        status = "Very Large, High Value, Discounted, Priority Order";
                    }
                }
                else
                {
                    status = "Error: No items in order";
                }
            }
            else
            {
                status = "Error: Null order";
            }

            return status;
        }
    }

    // Dummy classes for compilation
    public class Order
    {
        public Guid OrderId { get; set; }
        public List<OrderItem> Items { get; set; }
        public Address ShippingAddress { get; set; }
    }

    public class OrderItem
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public bool IsDiscounted { get; set; }
        public double DiscountPercentage { get; set; }
        public bool IsPriority { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string ZipCode { get; set; }
    }
}

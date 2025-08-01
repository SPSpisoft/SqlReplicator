//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Threading.Tasks;
//using Dapper;
//using Microsoft.Data.SqlClient;
//using Newtonsoft.Json;

//// =================================================================================
//// توجه: این یک فایل کامل و جامع است. شما باید اطلاعات اتصال به دیتابیس و API
//// را در بخش‌های مشخص شده وارد کنید.
//// =================================================================================

//#region API and Database Configuration (پیکربندی API و دیتابیس)

///// <summary>
///// کلاسی برای نگهداری اطلاعات اتصال به API ووکامرس
///// </summary>
//public static class WooCommerceApiHelper
//{
//    // TODO: آدرس سایت ووکامرس خود را اینجا وارد کنید
//    public static string StoreUrl { get; set; } = "https://your-store.com";

//    // TODO: کلیدهای API ووکامرس خود را اینجا وارد کنید
//    public static string ConsumerKey { get; set; } = "ck_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
//    public static string ConsumerSecret { get; set; } = "cs_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

//    public static string GetApiUrl(string endpoint)
//    {
//        return $"{StoreUrl}/wp-json/wc/v3/{endpoint}?consumer_key={ConsumerKey}&consumer_secret={ConsumerSecret}";
//    }
//}

///// <summary>
///// کلاسی برای مدیریت اتصال به دیتابیس محلی
///// </summary>
//public static class DatabaseService
//{
//    // TODO: رشته اتصال به دیتابیس SQL Server خود را اینجا وارد کنید
//    private static readonly string ConnectionString = "Server=.;Database=YourStagingDB;Trusted_Connection=True;TrustServerCertificate=True;";

//    public static IDbConnection CreateConnection()
//    {
//        return new SqlConnection(ConnectionString);
//    }
//}

//#endregion

//#region WooCommerce API Models (مدل‌های داده برای API)

//// این مدل‌ها ساختار JSON مورد انتظار API ووکامرس را نشان می‌دهند

//public class ProductDto
//{
//    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
//    public int? Id { get; set; }
//    [JsonProperty("name")]
//    public string Name { get; set; }
//    [JsonProperty("type")]
//    public string Type { get; set; }
//    [JsonProperty("status")]
//    public string Status { get; set; }
//    [JsonProperty("sku")]
//    public string Sku { get; set; }
//    [JsonProperty("regular_price")]
//    public string RegularPrice { get; set; }
//    [JsonProperty("sale_price")]
//    public string SalePrice { get; set; }
//    [JsonProperty("description")]
//    public string Description { get; set; }
//    [JsonProperty("short_description")]
//    public string ShortDescription { get; set; }
//    [JsonProperty("manage_stock")]
//    public bool ManageStock { get; set; }
//    [JsonProperty("stock_quantity", NullValueHandling = NullValueHandling.Ignore)]
//    public int? StockQuantity { get; set; }
//    [JsonProperty("stock_status")]
//    public string StockStatus { get; set; }
//    [JsonProperty("images")]
//    public List<ImageDto> Images { get; set; } = new List<ImageDto>();
//}

//public class ImageDto
//{
//    [JsonProperty("src")]
//    public string Src { get; set; }
//    [JsonProperty("position")]
//    public int Position { get; set; }
//}

//public class CustomerDto
//{
//    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
//    public int? Id { get; set; }
//    [JsonProperty("email")]
//    public string Email { get; set; }
//    [JsonProperty("first_name")]
//    public string FirstName { get; set; }
//    [JsonProperty("last_name")]
//    public string LastName { get; set; }
//    [JsonProperty("username")]
//    public string Username { get; set; }
//    [JsonProperty("billing")]
//    public AddressDto Billing { get; set; }
//    [JsonProperty("shipping")]
//    public AddressDto Shipping { get; set; }
//}

//public class OrderDto
//{
//    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
//    public int? Id { get; set; }
//    [JsonProperty("customer_id")]
//    public int CustomerId { get; set; }
//    [JsonProperty("status")]
//    public string Status { get; set; }
//    [JsonProperty("currency")]
//    public string Currency { get; set; }
//    [JsonProperty("payment_method")]
//    public string PaymentMethod { get; set; }
//    [JsonProperty("payment_method_title")]
//    public string PaymentMethodTitle { get; set; }
//    [JsonProperty("transaction_id")]
//    public string TransactionId { get; set; }
//    [JsonProperty("billing")]
//    public AddressDto Billing { get; set; }
//    [JsonProperty("shipping")]
//    public AddressDto Shipping { get; set; }
//    [JsonProperty("line_items")]
//    public List<LineItemDto> LineItems { get; set; }
//}

//public class LineItemDto
//{
//    [JsonProperty("product_id")]
//    public int ProductId { get; set; }
//    [JsonProperty("quantity")]
//    public int Quantity { get; set; }
//    [JsonProperty("variation_id", NullValueHandling = NullValueHandling.Ignore)]
//    public int? VariationId { get; set; }
//}

//public class AddressDto
//{
//    [JsonProperty("first_name")]
//    public string FirstName { get; set; }
//    [JsonProperty("last_name")]
//    public string LastName { get; set; }
//    [JsonProperty("company")]
//    public string Company { get; set; }
//    [JsonProperty("address_1")]
//    public string Address1 { get; set; }
//    [JsonProperty("address_2")]
//    public string Address2 { get; set; }
//    [JsonProperty("city")]
//    public string City { get; set; }
//    [JsonProperty("state")]
//    public string State { get; set; }
//    [JsonProperty("postcode")]
//    public string Postcode { get; set; }
//    [JsonProperty("country")]
//    public string Country { get; set; }
//    [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)]
//    public string Email { get; set; }
//    [JsonProperty("phone")]
//    public string Phone { get; set; }
//}

//#endregion

//#region Local Database Models (مدل‌های داده برای دیتابیس محلی)

//public class LocalProduct
//{
//    public int id { get; set; }
//    public int? woocommerce_id { get; set; }
//    public string name { get; set; }
//    public string type { get; set; }
//    public string status { get; set; }
//    public string sku { get; set; }
//    public decimal? regular_price { get; set; }
//    public decimal? sale_price { get; set; }
//    public string description { get; set; }
//    public string short_description { get; set; }
//    public bool manage_stock { get; set; }
//    public int? stock_quantity { get; set; }
//    public string stock_status { get; set; }
//}

//public class LocalProductImage
//{
//    public string image_url { get; set; }
//    public int position { get; set; }
//}

//public class LocalCustomer
//{
//    public int id { get; set; }
//    public int? woocommerce_id { get; set; }
//    public string email { get; set; }
//    public string first_name { get; set; }
//    public string last_name { get; set; }
//    public string username { get; set; }
//    public string billing_first_name { get; set; }
//    public string billing_last_name { get; set; }
//    public string billing_company { get; set; }
//    public string billing_address_1 { get; set; }
//    public string billing_address_2 { get; set; }
//    public string billing_city { get; set; }
//    public string billing_state { get; set; }
//    public string billing_postcode { get; set; }
//    public string billing_country { get; set; }
//    public string billing_phone { get; set; }
//    public string shipping_first_name { get; set; }
//    public string shipping_last_name { get; set; }
//    public string shipping_company { get; set; }
//    public string shipping_address_1 { get; set; }
//    public string shipping_address_2 { get; set; }
//    public string shipping_city { get; set; }
//    public string shipping_state { get; set; }
//    public string shipping_postcode { get; set; }
//    public string shipping_country { get; set; }
//}

//public class LocalOrder
//{
//    public int id { get; set; }
//    public int? woocommerce_id { get; set; }
//    public string status { get; set; }
//    public string currency { get; set; }
//    public int? customer_local_id { get; set; }
//    public string payment_method_id { get; set; }
//    public string payment_method_title { get; set; }
//    public string transaction_id { get; set; }
//}

//public class LocalOrderItem
//{
//    public int product_woocommerce_id { get; set; }
//    public int? variation_woocommerce_id { get; set; }
//    public int quantity { get; set; }
//}


//#endregion

///// <summary>
///// سرویس اصلی برای همگام‌سازی داده‌ها با ووکامرس
///// </summary>
//public class WooCommerceSyncService
//{
//    private readonly HttpClient _httpClient;

//    public WooCommerceSyncService()
//    {
//        _httpClient = new HttpClient();
//    }

//    #region Product Sync (همگام‌سازی محصولات)

//    public async Task SyncProductById(int localProductId)
//    {
//        await SyncEntityById(
//            localId: localProductId,
//            fetchEntitySql: "SELECT * FROM tbl_products WHERE id = @Id",
//            fetchRelatedSql: "SELECT image_url, position FROM tbl_product_images WHERE product_local_id = @Id ORDER BY position",
//            mapToDto: (LocalProduct p, List<LocalProductImage> images) => new ProductDto
//            {
//                Name = p.name,
//                Type = p.type,
//                Status = p.status,
//                Sku = p.sku,
//                RegularPrice = p.regular_price?.ToString("F2"),
//                SalePrice = string.IsNullOrEmpty(p.sale_price?.ToString()) ? "" : p.sale_price?.ToString("F2"),
//                Description = p.description,
//                ShortDescription = p.short_description,
//                ManageStock = p.manage_stock,
//                StockQuantity = p.manage_stock ? p.stock_quantity : null,
//                StockStatus = p.stock_status,
//                Images = images.Select(img => new ImageDto { Src = img.image_url, Position = img.position }).ToList()
//            },
//            getWooCommerceId: p => p.woocommerce_id,
//            endpoint: "products",
//            updateLocalStatusSql: "UPDATE tbl_products SET sync_status = @Status, error_message = @ErrorMessage, last_sync_attempt = GETDATE(), woocommerce_id = ISNULL(@WooCommerceId, woocommerce_id) WHERE id = @LocalId",
//            entityName: "محصول"
//        );
//    }

//    #endregion

//    #region Customer Sync (همگام‌سازی مشتریان)

//    public async Task SyncCustomerById(int localCustomerId)
//    {
//        await SyncEntityById<LocalCustomer, object, CustomerDto>(
//            localId: localCustomerId,
//            fetchEntitySql: "SELECT * FROM tbl_customers WHERE id = @Id",
//            fetchRelatedSql: null, // مشتریان داده مرتبطی در جدول دیگر ندارند
//            mapToDto: (LocalCustomer c, List<object> _) => new CustomerDto
//            {
//                Email = c.email,
//                FirstName = c.first_name,
//                LastName = c.last_name,
//                Username = c.username,
//                Billing = new AddressDto
//                {
//                    FirstName = c.billing_first_name,
//                    LastName = c.billing_last_name,
//                    Company = c.billing_company,
//                    Address1 = c.billing_address_1,
//                    Address2 = c.billing_address_2,
//                    City = c.billing_city,
//                    State = c.billing_state,
//                    Postcode = c.billing_postcode,
//                    Country = c.billing_country,
//                    Phone = c.billing_phone,
//                    Email = c.email
//                },
//                Shipping = new AddressDto
//                {
//                    FirstName = c.shipping_first_name,
//                    LastName = c.shipping_last_name,
//                    Company = c.shipping_company,
//                    Address1 = c.shipping_address_1,
//                    Address2 = c.shipping_address_2,
//                    City = c.shipping_city,
//                    State = c.shipping_state,
//                    Postcode = c.shipping_postcode,
//                    Country = c.shipping_country
//                }
//            },
//            getWooCommerceId: c => c.woocommerce_id,
//            endpoint: "customers",
//            updateLocalStatusSql: "UPDATE tbl_customers SET sync_status = @Status, error_message = @ErrorMessage, last_sync_attempt = GETDATE(), woocommerce_id = ISNULL(@WooCommerceId, woocommerce_id) WHERE id = @LocalId",
//            entityName: "مشتری"
//        );
//    }

//    #endregion

//    #region Order Sync (همگام‌سازی سفارش‌ها)

//    public async Task SyncOrderById(int localOrderId)
//    {
//        // برای سفارش‌ها به دلیل نیاز به گرفتن شناسه مشتری از ووکامرس، منطق کمی متفاوت است
//        try
//        {
//            LocalOrder order;
//            List<LocalOrderItem> items;
//            int? customerWooCommerceId = null;

//            using (var connection = DatabaseService.CreateConnection())
//            {
//                order = await connection.QuerySingleOrDefaultAsync<LocalOrder>("SELECT * FROM tbl_orders WHERE id = @Id", new { Id = localOrderId });
//                if (order == null)
//                {
//                    Console.WriteLine($"سفارشی با شناسه داخلی {localOrderId} یافت نشد.");
//                    return;
//                }
//                items = (await connection.QueryAsync<LocalOrderItem>("SELECT * FROM tbl_order_items WHERE order_local_id = @Id", new { Id = localOrderId })).ToList();

//                if (order.customer_local_id.HasValue)
//                {
//                    customerWooCommerceId = await connection.QuerySingleOrDefaultAsync<int?>(
//                        "SELECT woocommerce_id FROM tbl_customers WHERE id = @Id", new { Id = order.customer_local_id.Value });

//                    if (!customerWooCommerceId.HasValue)
//                    {
//                        await UpdateLocalOrderStatus(localOrderId, null, "error", "مشتری این سفارش هنوز با ووکامرس همگام‌سازی نشده است.");
//                        Console.WriteLine($"همگام‌سازی سفارش {localOrderId} شکست خورد: مشتری همگام‌سازی نشده است.");
//                        return;
//                    }
//                }
//            }

//            // فرض می‌کنیم آدرس‌ها از مشتری گرفته می‌شود یا در سفارش ذخیره شده‌اند
//            // برای سادگی، در این مثال آدرس‌ها را خالی می‌گذاریم
//            var orderDto = new OrderDto
//            {
//                CustomerId = customerWooCommerceId ?? 0,
//                Status = order.status,
//                Currency = order.currency,
//                PaymentMethod = order.payment_method_id,
//                PaymentMethodTitle = order.payment_method_title,
//                TransactionId = order.transaction_id,
//                LineItems = items.Select(i => new LineItemDto
//                {
//                    ProductId = i.product_woocommerce_id,
//                    VariationId = i.variation_woocommerce_id,
//                    Quantity = i.quantity
//                }).ToList()
//            };

//            string jsonPayload = JsonConvert.SerializeObject(orderDto, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
//            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
//            HttpResponseMessage response;

//            if (order.woocommerce_id.HasValue && order.woocommerce_id > 0)
//            {
//                string updateUrl = WooCommerceApiHelper.GetApiUrl($"orders/{order.woocommerce_id}");
//                response = await _httpClient.PutAsync(updateUrl, content);
//            }
//            else
//            {
//                string createUrl = WooCommerceApiHelper.GetApiUrl("orders");
//                response = await _httpClient.PostAsync(createUrl, content);
//            }

//            string responseBody = await response.Content.ReadAsStringAsync();
//            if (response.IsSuccessStatusCode)
//            {
//                var responseDto = JsonConvert.DeserializeObject<OrderDto>(responseBody);
//                await UpdateLocalOrderStatus(localOrderId, responseDto.Id, "synced", null);
//                Console.WriteLine($"سفارش با شناسه داخلی {localOrderId} با موفقیت همگام‌سازی شد. شناسه ووکامرس: {responseDto.Id}");
//            }
//            else
//            {
//                await UpdateLocalOrderStatus(localOrderId, null, "error", responseBody);
//                Console.WriteLine($"خطا در همگام‌سازی سفارش با شناسه داخلی {localOrderId}. پاسخ سرور: {responseBody}");
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"خطای غیرمنتظره در همگام‌سازی سفارش {localOrderId}: {ex.Message}");
//            await UpdateLocalOrderStatus(localOrderId, null, "error", ex.Message);
//        }
//    }

//    private async Task UpdateLocalOrderStatus(int localId, int? wooCommerceId, string status, string errorMessage)
//    {
//        await UpdateLocalEntityStatus(
//            "UPDATE tbl_orders SET sync_status = @Status, error_message = @ErrorMessage, last_sync_attempt = GETDATE(), woocommerce_id = ISNULL(@WooCommerceId, woocommerce_id) WHERE id = @LocalId",
//            localId,
//            wooCommerceId,
//            status,
//            errorMessage
//        );
//    }

//    #endregion

//    #region Generic Sync Logic (منطق همگام‌سازی عمومی)

//    /// <summary>
//    /// یک متد عمومی برای همگام‌سازی موجودیت‌ها (محصولات، مشتریان و غیره)
//    /// </summary>
//    private async Task SyncEntityById<TLocal, TRelated, TDto>(
//        int localId,
//        string fetchEntitySql,
//        string fetchRelatedSql,
//        Func<TLocal, List<TRelated>, TDto> mapToDto,
//        Func<TLocal, int?> getWooCommerceId,
//        string endpoint,
//        string updateLocalStatusSql,
//        string entityName) where TLocal : class where TRelated : class
//    {
//        try
//        {
//            TLocal entity;
//            List<TRelated> relatedEntities = new List<TRelated>();

//            using (var connection = DatabaseService.CreateConnection())
//            {
//                entity = await connection.QuerySingleOrDefaultAsync<TLocal>(fetchEntitySql, new { Id = localId });
//                if (entity == null)
//                {
//                    Console.WriteLine($"{entityName} با شناسه داخلی {localId} یافت نشد.");
//                    return;
//                }

//                if (!string.IsNullOrEmpty(fetchRelatedSql))
//                {
//                    relatedEntities = (await connection.QueryAsync<TRelated>(fetchRelatedSql, new { Id = localId })).ToList();
//                }
//            }

//            var dto = mapToDto(entity, relatedEntities);
//            string jsonPayload = JsonConvert.SerializeObject(dto, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
//            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
//            HttpResponseMessage response;
//            int? wooCommerceId = getWooCommerceId(entity);

//            if (wooCommerceId.HasValue && wooCommerceId > 0)
//            {
//                string updateUrl = WooCommerceApiHelper.GetApiUrl($"{endpoint}/{wooCommerceId}");
//                response = await _httpClient.PutAsync(updateUrl, content);
//            }
//            else
//            {
//                string createUrl = WooCommerceApiHelper.GetApiUrl(endpoint);
//                response = await _httpClient.PostAsync(createUrl, content);
//            }

//            string responseBody = await response.Content.ReadAsStringAsync();
//            if (response.IsSuccessStatusCode)
//            {
//                dynamic responseDto = JsonConvert.DeserializeObject(responseBody);
//                int newWooCommerceId = responseDto.id;
//                await UpdateLocalEntityStatus(updateLocalStatusSql, localId, newWooCommerceId, "synced", null);
//                Console.WriteLine($"{entityName} با شناسه داخلی {localId} با موفقیت همگام‌سازی شد. شناسه ووکامرس: {newWooCommerceId}");
//            }
//            else
//            {
//                await UpdateLocalEntityStatus(updateLocalStatusSql, localId, null, "error", responseBody);
//                Console.WriteLine($"خطا در همگام‌سازی {entityName} با شناسه داخلی {localId}. پاسخ سرور: {responseBody}");
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"خطای غیرمنتظره در همگام‌سازی {entityName} {localId}: {ex.Message}");
//            await UpdateLocalEntityStatus(updateLocalStatusSql, localId, null, "error", ex.Message);
//        }
//    }

//    private async Task UpdateLocalEntityStatus(string sql, int localId, int? wooCommerceId, string status, string errorMessage)
//    {
//        using (var connection = DatabaseService.CreateConnection())
//        {
//            await connection.ExecuteAsync(sql, new
//            {
//                Status = status,
//                ErrorMessage = errorMessage,
//                WooCommerceId = wooCommerceId,
//                LocalId = localId
//            });
//        }
//    }

//    #endregion
//}

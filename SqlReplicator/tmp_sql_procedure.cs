//using System;
//using System.Collections.Generic;
//using System.Diagnostics.Metrics;
//using System.Text;

//USE[WooCommerceStagingDB]
//GO
///****** Object:  StoredProcedure [dbo].[sp_CreateWooCommerceStagingDB]    Script Date: 17/07/2025 00:44:26 ******/
//SET ANSI_NULLS ON
//GO
//SET QUOTED_IDENTIFIER ON
//GO
//-- =================================================================================
//-- Stored Procedure: sp_CreateWooCommerceStagingDB
//-- Description: این پروسیجر تمام جداول لازم برای دیتابیس واسط (Staging)
//--                  جهت همگام‌سازی با API ووکامرس را ایجاد می‌کند. همچنین یک جدول
//--                  مستندات (`tbl_field_documentation`) ساخته و آن را با توضیحات
//--                  فارسی و انگلیسی تمام فیلدها پر می‌کند.
//-- Author:          Gemini
//-- Version: 2.1(Fixed Persian character encoding issue)
//-- =================================================================================
//ALTER   PROCEDURE[dbo].[sp_CreateWooCommerceStagingDB]
//AS
//BEGIN
//    -- جلوگیری از نمایش پیام تعداد ردیف‌های تحت تأثیر
//    SET NOCOUNT ON;

//-- =================================================================
//--مرحله ۱: حذف جداول موجود (برای اجرای مجدد و بدون خطای پروسیجر)
//    -- =================================================================
//    IF OBJECT_ID('tbl_field_documentation', 'U') IS NOT NULL DROP TABLE tbl_field_documentation;
//IF OBJECT_ID('tbl_order_items', 'U') IS NOT NULL DROP TABLE tbl_order_items;
//IF OBJECT_ID('tbl_orders', 'U') IS NOT NULL DROP TABLE tbl_orders;
//IF OBJECT_ID('tbl_customers', 'U') IS NOT NULL DROP TABLE tbl_customers;
//IF OBJECT_ID('tbl_product_variations', 'U') IS NOT NULL DROP TABLE tbl_product_variations;
//IF OBJECT_ID('tbl_product_images', 'U') IS NOT NULL DROP TABLE tbl_product_images;
//IF OBJECT_ID('tbl_product_categories', 'U') IS NOT NULL DROP TABLE tbl_product_categories;
//IF OBJECT_ID('tbl_products', 'U') IS NOT NULL DROP TABLE tbl_products;


//-- =================================================================
//--مرحله ۲: ساخت جدول مستندات فیلدها
//    -- =================================================================
//    CREATE TABLE tbl_field_documentation (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    TableName NVARCHAR(128) NOT NULL,
//    FieldName NVARCHAR(128) NOT NULL,
//    DataType NVARCHAR(128) NOT NULL,
//    DescriptionEN NVARCHAR(1000) NOT NULL,
//    DescriptionFA NVARCHAR(1000) NOT NULL,
//    IsSystemManaged BIT NOT NULL DEFAULT 0
//    );

//-- =================================================================
//--مرحله ۳: ساخت جدول محصولات (tbl_products) و مستندات آن
//    -- =================================================================
//    CREATE TABLE tbl_products (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    woocommerce_id INT NULL,
//    name NVARCHAR(255) NOT NULL,
//    slug NVARCHAR(255) NULL,
//    type NVARCHAR(50) DEFAULT 'simple',
//    status NVARCHAR(50) DEFAULT 'publish',
//    featured BIT DEFAULT 0,
//    catalog_visibility NVARCHAR(50) DEFAULT 'visible',
//    description NTEXT NULL,
//    short_description NTEXT NULL,
//    sku NVARCHAR(100) NULL,
//    regular_price DECIMAL(18, 4) NULL,
//    sale_price DECIMAL(18, 4) NULL,
//    date_on_sale_from DATETIME NULL,
//    date_on_sale_to DATETIME NULL,
//    stock_quantity INT NULL,
//    manage_stock BIT DEFAULT 0,
//    stock_status NVARCHAR(50) DEFAULT 'instock',
//    backorders NVARCHAR(50) DEFAULT 'no',
//    weight DECIMAL(10, 2) NULL,
//    length DECIMAL(10, 2) NULL,
//    width DECIMAL(10, 2) NULL,
//    height DECIMAL(10, 2) NULL,
//    sync_status NVARCHAR(50) DEFAULT 'pending',
//    last_sync_attempt DATETIME NULL,
//    error_message NTEXT NULL
//    );

//--پر کردن مستندات برای جدول tbl_products با پیشوند N برای رشته‌های فارسی
//    INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_products', 'id', 'INT', 'Internal unique identifier. Not to be mapped.', N'شناسه یکتای داخلی. نیازی به مپ کردن ندارد.', 1),
//    ('tbl_products', 'woocommerce_id', 'INT', 'Product ID from WooCommerce. Stored after a successful sync. Used for updates. Not to be mapped.', N'شناسه محصول در ووکامرس. پس از اولین همگام‌سازی موفق، اینجا ذخیره می‌شود. برای آپدیت‌ها استفاده می‌شود. نیازی به مپ کردن ندارد.', 1),
//    ('tbl_products', 'name', 'NVARCHAR(255)', 'The full name of the product displayed to the customer.', N'نام کامل محصول که به مشتری نمایش داده می‌شود (مثال: پیراهن مردانه نخی).', 0),
//    ('tbl_products', 'slug', 'NVARCHAR(255)', 'URL-friendly version of the name. If left empty, WooCommerce generates it automatically.', N'بخش URL-friendly نام محصول (مثال: pirahan-mardane-nakhi). اگر خالی باشد، ووکامرس خودکار می‌سازد.', 0),
//    ('tbl_products', 'type', 'NVARCHAR(50)', 'Product type. Common values: simple, variable, grouped.', N'نوع محصول. مقادیر متداول: simple (ساده)، variable (متغیر)، grouped (گروهی).', 0),
//    ('tbl_products', 'status', 'NVARCHAR(50)', 'Product publication status. Common values: publish, draft.', N'وضعیت انتشار محصول در سایت. مقادیر متداول: publish (منتشر شده)، draft (پیش‌نویس).', 0),
//    ('tbl_products', 'featured', 'BIT', 'Determines if the product is featured. (1 for yes, 0 for no).', N'آیا محصول در لیست محصولات ویژه نمایش داده شود؟ (1 برای بله، 0 برای خیر).', 0),
//    ('tbl_products', 'catalog_visibility', 'NVARCHAR(50)', 'How the product is displayed. Values: visible, catalog, search, hidden.', N'نحوه نمایش در کاتالوگ. مقادیر: visible (نمایش همه‌جا)، catalog (فقط در صفحات فروشگاه)، search (فقط در نتایج جستجو)، hidden (مخفی).', 0),
//    ('tbl_products', 'description', 'NTEXT', 'The main, long description of the product (can include HTML).', N'توضیحات کامل و بلند محصول (می‌تواند شامل کدهای HTML باشد).', 0),
//    ('tbl_products', 'short_description', 'NTEXT', 'A short summary of the product, usually displayed below the price.', N'خلاصه توضیحات محصول که معمولاً زیر قیمت نمایش داده می‌شود.', 0),
//    ('tbl_products', 'sku', 'NVARCHAR(100)', 'Stock Keeping Unit. A unique code to identify the product in your inventory.', N'شناسه انبارداری (SKU). کد منحصر به فرد برای شناسایی محصول در انبار شما.', 0),
//    ('tbl_products', 'regular_price', 'DECIMAL(18, 4)', 'The standard price of the product.', N'قیمت اصلی و عادی محصول.', 0),
//    ('tbl_products', 'sale_price', 'DECIMAL(18, 4)', 'The discounted price during a sale. Must be less than the regular price.', N'قیمت فروش ویژه (در زمان حراج). باید کمتر از قیمت اصلی باشد.', 0),
//    ('tbl_products', 'date_on_sale_from', 'DATETIME', 'The start date and time of the sale.', N'تاریخ و زمان شروع حراج.', 0),
//    ('tbl_products', 'date_on_sale_to', 'DATETIME', 'The end date and time of the sale.', N'تاریخ و زمان پایان حراج.', 0),
//    ('tbl_products', 'stock_quantity', 'INT', 'The current stock level. Only used if manage_stock is enabled.', N'تعداد موجودی انبار. تنها در صورتی که manage_stock فعال باشد، کاربرد دارد.', 0),
//    ('tbl_products', 'manage_stock', 'BIT', 'Enable stock management for this product? (1 for yes, 0 for no).', N'آیا مدیریت انبار برای این محصول فعال باشد؟ (1 برای بله، 0 برای خیر).', 0),
//    ('tbl_products', 'stock_status', 'NVARCHAR(50)', 'The stock status. Values: instock, outofstock, onbackorder.', N'وضعیت موجودی. مقادیر: instock (موجود)، outofstock (ناموجود)، onbackorder (پیش‌سفارش).', 0),
//    ('tbl_products', 'backorders', 'NVARCHAR(50)', 'Can the product be backordered? Values: no, notify, yes.', N'آیا امکان پیش‌سفارش در صورت ناموجود بودن وجود دارد؟ مقادیر: no (خیر)، notify (اطلاع‌رسانی)، yes (بله).', 0),
//    ('tbl_products', 'weight', 'DECIMAL(10, 2)', 'The weight of the product (for shipping calculation). Default unit is kg.', N'وزن محصول (برای محاسبه هزینه ارسال). واحد پیش‌فرض کیلوگرم است.', 0),
//    ('tbl_products', 'length', 'DECIMAL(10, 2)', 'The length of the product (for shipping calculation). Default unit is cm.', N'طول محصول (برای محاسبه هزینه ارسال). واحد پیش‌فرض سانتی‌متر است.', 0),
//    ('tbl_products', 'width', 'DECIMAL(10, 2)', 'The width of the product (for shipping calculation). Default unit is cm.', N'عرض محصول (برای محاسبه هزینه ارسال). واحد پیش‌فرض سانتی‌متر است.', 0),
//    ('tbl_products', 'height', 'DECIMAL(10, 2)', 'The height of the product (for shipping calculation). Default unit is cm.', N'ارتفاع محصول (برای محاسبه هزینه ارسال). واحد پیش‌فرض سانتی‌متر است.', 0),
//    ('tbl_products', 'sync_status', 'NVARCHAR(50)', 'Sync status (pending, synced, error). Not to be mapped.', N'وضعیت همگام‌سازی (pending, synced, error). نیازی به مپ کردن ندارد.', 1),
//    ('tbl_products', 'last_sync_attempt', 'DATETIME', 'Timestamp of the last sync attempt. Not to be mapped.', N'زمان آخرین تلاش برای همگام‌سازی. نیازی به مپ کردن ندارد.', 1),
//    ('tbl_products', 'error_message', 'NTEXT', 'Error message from the API on sync failure. Not to be mapped.', N'پیام خطای دریافت شده از API در صورت شکست همگام‌سازی. نیازی به مپ کردن ندارد.', 1);

//-- =================================================================
//--مرحله ۴: ساخت جداول مرتبط با محصولات و مستندات آنها
//    -- =================================================================
//    CREATE TABLE tbl_product_categories (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    product_local_id INT NOT NULL,
//    category_woocommerce_id INT NOT NULL,
//    CONSTRAINT FK_ProductCategories_Products FOREIGN KEY (product_local_id) REFERENCES tbl_products(id) ON DELETE CASCADE
//    );
//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_product_categories', 'id', 'INT', 'Internal unique identifier.', N'شناسه یکتای داخلی.', 1),
//    ('tbl_product_categories', 'product_local_id', 'INT', 'The ID of the product in tbl_products that this category belongs to.', N'شناسه محصول در جدول tbl_products که این دسته‌بندی به آن تعلق دارد.', 0),
//    ('tbl_product_categories', 'category_woocommerce_id', 'INT', 'The ID of the category from the WooCommerce site. (These IDs must be pre-existing on the site).', N'شناسه دسته‌بندی در سایت ووکامرس. (این شناسه‌ها باید از قبل در سایت ووکامرس ساخته شده و در نرم‌افزار شما موجود باشند).', 0);

//CREATE TABLE tbl_product_images (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    product_local_id INT NOT NULL,
//    image_url NVARCHAR(MAX) NOT NULL,
//    position INT DEFAULT 0,
//    CONSTRAINT FK_ProductImages_Products FOREIGN KEY (product_local_id) REFERENCES tbl_products(id) ON DELETE CASCADE
//    );
//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_product_images', 'id', 'INT', 'Internal unique identifier.', N'شناسه یکتای داخلی.', 1),
//    ('tbl_product_images', 'product_local_id', 'INT', 'The ID of the product in tbl_products that this image belongs to.', N'شناسه محصول در جدول tbl_products که این تصویر به آن تعلق دارد.', 0),
//    ('tbl_product_images', 'image_url', 'NVARCHAR(MAX)', 'The full, publicly accessible URL of the image.', N'آدرس اینترنتی کامل و قابل دسترس تصویر (مثال: https://example.com/image.jpg).', 0),
//    ('tbl_product_images', 'position', 'INT', 'The display order of the images. 0 is the main/featured image.', N'ترتیب نمایش تصاویر. عدد 0 به معنی تصویر اصلی و شاخص محصول است. اعداد بعدی ترتیب سایر تصاویر را مشخص می‌کنند.', 0);

//CREATE TABLE tbl_product_variations (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    woocommerce_id INT NULL,
//    product_local_id INT NOT NULL,
//    sku NVARCHAR(100) NULL,
//    regular_price DECIMAL(18, 4) NULL,
//    sale_price DECIMAL(18, 4) NULL,
//    stock_quantity INT NULL,
//    image_url NVARCHAR(MAX) NULL,
//    attributes NVARCHAR(MAX) NOT NULL,
//    CONSTRAINT FK_ProductVariations_Products FOREIGN KEY (product_local_id) REFERENCES tbl_products(id) ON DELETE CASCADE
//    );
//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_product_variations', 'id', 'INT', 'Internal unique identifier.', N'شناسه یکتای داخلی.', 1),
//    ('tbl_product_variations', 'woocommerce_id', 'INT', 'The ID of this specific variation in WooCommerce. Filled after sync.', N'شناسه این متغیر خاص در ووکامرس. پس از همگام‌سازی پر می‌شود.', 1),
//    ('tbl_product_variations', 'product_local_id', 'INT', 'The ID of the parent variable product in tbl_products.', N'شناسه محصول اصلی (که از نوع variable است) در جدول tbl_products.', 0),
//    ('tbl_product_variations', 'sku', 'NVARCHAR(100)', 'The Stock Keeping Unit (SKU) for this specific variation.', N'شناسه انبارداری (SKU) برای این متغیر خاص.', 0),
//    ('tbl_product_variations', 'regular_price', 'DECIMAL(18, 4)', 'The standard price for this specific variation.', N'قیمت عادی این متغیر خاص.', 0),
//    ('tbl_product_variations', 'sale_price', 'DECIMAL(18, 4)', 'The sale price for this specific variation.', N'قیمت حراج این متغیر خاص.', 0),
//    ('tbl_product_variations', 'stock_quantity', 'INT', 'The stock quantity for this specific variation.', N'موجودی انبار برای این متغیر خاص.', 0),
//    ('tbl_product_variations', 'image_url', 'NVARCHAR(MAX)', 'URL of an image specific to this variation (e.g., image of a red t-shirt).', N'آدرس تصویر مخصوص این متغیر (مثلاً تصویر تیشرت قرمز).', 0),
//    ('tbl_product_variations', 'attributes', 'NVARCHAR(MAX)', 'IMPORTANT: The attributes for this variation in JSON format. Example: [{"id": 1, "option": "Blue"}, {"id": 2, "option": "Large"}]', N'مهم: ویژگی‌های این متغیر در قالب JSON. مثال: [{"id": 1, "option": "Blue"}, {"id": 2, "option": "Large"}]', 0);

//-- =================================================================
//--مرحله ۵: ساخت جدول مشتریان (tbl_customers) و مستندات آن
//    -- =================================================================
//    CREATE TABLE tbl_customers (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    woocommerce_id INT NULL,
//    email NVARCHAR(255) NOT NULL,
//    first_name NVARCHAR(255) NULL,
//    last_name NVARCHAR(255) NULL,
//    username NVARCHAR(255) NOT NULL,
//    billing_first_name NVARCHAR(255) NULL,
//    billing_last_name NVARCHAR(255) NULL,
//    billing_company NVARCHAR(255) NULL,
//    billing_address_1 NVARCHAR(255) NULL,
//    billing_address_2 NVARCHAR(255) NULL,
//    billing_city NVARCHAR(255) NULL,
//    billing_state NVARCHAR(100) NULL,
//    billing_postcode NVARCHAR(50) NULL,
//    billing_country NVARCHAR(100) NULL,
//    billing_phone NVARCHAR(100) NULL,
//    shipping_first_name NVARCHAR(255) NULL,
//    shipping_last_name NVARCHAR(255) NULL,
//    shipping_company NVARCHAR(255) NULL,
//    shipping_address_1 NVARCHAR(255) NULL,
//    shipping_address_2 NVARCHAR(255) NULL,
//    shipping_city NVARCHAR(255) NULL,
//    shipping_state NVARCHAR(100) NULL,
//    shipping_postcode NVARCHAR(50) NULL,
//    shipping_country NVARCHAR(100) NULL
//    );

//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_customers', 'id', 'INT', 'Internal unique identifier for the customer.', N'شناسه یکتای داخلی مشتری.', 1),
//    ('tbl_customers', 'woocommerce_id', 'INT', 'Customer ID from WooCommerce. Filled after sync.', N'شناسه مشتری در ووکامرس. پس از همگام‌سازی پر می‌شود.', 1),
//    ('tbl_customers', 'email', 'NVARCHAR(255)', 'The customer''s email address (must be unique).', N'ایمیل مشتری (باید یکتا باشد).', 0),
//    ('tbl_customers', 'first_name', 'NVARCHAR(255)', 'The customer''s first name.', N'نام کوچک مشتری.', 0),
//    ('tbl_customers', 'last_name', 'NVARCHAR(255)', 'The customer''s last name.', N'نام خانوادگی مشتری.', 0),
//    ('tbl_customers', 'username', 'NVARCHAR(255)', 'The customer''s username for logging into the site (must be unique).', N'نام کاربری مشتری برای ورود به سایت (باید یکتا باشد).', 0),
//    ('tbl_customers', 'billing_first_name', 'NVARCHAR(255)', 'First name for the billing address.', N'نام کوچک در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_last_name', 'NVARCHAR(255)', 'Last name for the billing address.', N'نام خانوادگی در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_company', 'NVARCHAR(255)', 'Company name for the billing address.', N'نام شرکت در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_address_1', 'NVARCHAR(255)', 'Main street address for billing.', N'آدرس اصلی صورتحساب (خیابان، پلاک).', 0),
//    ('tbl_customers', 'billing_address_2', 'NVARCHAR(255)', 'Secondary address line for billing (e.g., apartment, suite).', N'ادامه آدرس صورتحساب (واحد، طبقه).', 0),
//    ('tbl_customers', 'billing_city', 'NVARCHAR(255)', 'City for the billing address.', N'شهر در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_state', 'NVARCHAR(100)', 'State/Province for the billing address.', N'استان در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_postcode', 'NVARCHAR(50)', 'Postal code for the billing address.', N'کد پستی در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_country', 'NVARCHAR(100)', 'Country for the billing address.', N'کشور در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'billing_phone', 'NVARCHAR(100)', 'Phone number for the billing address.', N'شماره تلفن در آدرس صورتحساب.', 0),
//    ('tbl_customers', 'shipping_first_name', 'NVARCHAR(255)', 'First name for the shipping address.', N'نام کوچک در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_last_name', 'NVARCHAR(255)', 'Last name for the shipping address.', N'نام خانوادگی در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_company', 'NVARCHAR(255)', 'Company name for the shipping address.', N'نام شرکت در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_address_1', 'NVARCHAR(255)', 'Main street address for shipping.', N'آدرس اصلی حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_address_2', 'NVARCHAR(255)', 'Secondary address line for shipping.', N'ادامه آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_city', 'NVARCHAR(255)', 'City for the shipping address.', N'شهر در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_state', 'NVARCHAR(100)', 'State/Province for the shipping address.', N'استان در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_postcode', 'NVARCHAR(50)', 'Postal code for the shipping address.', N'کد پستی در آدرس حمل و نقل.', 0),
//    ('tbl_customers', 'shipping_country', 'NVARCHAR(100)', 'Country for the shipping address.', N'کشور در آدرس حمل و نقل.', 0);

//-- =================================================================
//--مرحله ۶: ساخت جداول سفارش‌ها (tbl_orders, tbl_order_items) و مستندات آنها
//    -- =================================================================
//    CREATE TABLE tbl_orders (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    woocommerce_id INT NULL,
//    status NVARCHAR(50) NOT NULL,
//    currency NVARCHAR(10) NOT NULL,
//    customer_local_id INT NULL,
//    customer_note NTEXT NULL,
//    total DECIMAL(18, 4) NOT NULL,
//    payment_method_id NVARCHAR(100) NULL,
//    payment_method_title NVARCHAR(255) NULL,
//    transaction_id NVARCHAR(255) NULL,
//    date_created DATETIME NULL,
//    CONSTRAINT FK_Orders_Customers FOREIGN KEY (customer_local_id) REFERENCES tbl_customers(id) ON DELETE SET NULL
//    );
//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_orders', 'id', 'INT', 'Internal unique identifier for the order.', N'شناسه یکتای داخلی سفارش.', 1),
//    ('tbl_orders', 'woocommerce_id', 'INT', 'Order ID from WooCommerce. Filled after sync.', N'شناسه سفارش در ووکامرس. پس از همگام‌سازی پر می‌شود.', 1),
//    ('tbl_orders', 'status', 'NVARCHAR(50)', 'The status of the order. Common values: processing, completed, cancelled.', N'وضعیت سفارش. مقادیر متداول: processing (در حال انجام), completed (تکمیل شده), cancelled (لغو شده).', 0),
//    ('tbl_orders', 'currency', 'NVARCHAR(10)', 'The currency of the order (e.g., IRT).', N'واحد پول سفارش (مثال: IRT).', 0),
//    ('tbl_orders', 'customer_local_id', 'INT', 'The ID of the customer in tbl_customers.', N'شناسه مشتری در جدول tbl_customers.', 0),
//    ('tbl_orders', 'customer_note', 'NTEXT', 'The note the customer added during checkout.', N'یادداشتی که مشتری هنگام ثبت سفارش نوشته است.', 0),
//    ('tbl_orders', 'total', 'DECIMAL(18, 4)', 'The final, total amount of the order.', N'مبلغ نهایی و کل سفارش.', 0),
//    ('tbl_orders', 'payment_method_id', 'NVARCHAR(100)', 'The system ID of the payment method (e.g., bacs).', N'شناسه سیستمی روش پرداخت (مثال: bacs).', 0),
//    ('tbl_orders', 'payment_method_title', 'NVARCHAR(255)', 'The title of the payment method shown to the user (e.g., Online Payment).', N'عنوان روش پرداخت که به کاربر نمایش داده شده (مثال: پرداخت آنلاین).', 0),
//    ('tbl_orders', 'transaction_id', 'NVARCHAR(255)', 'The bank transaction ID, if available.', N'شناسه تراکنش بانکی در صورت وجود.', 0),
//    ('tbl_orders', 'date_created', 'DATETIME', 'The date and time the order was created in the source system.', N'تاریخ و زمان ثبت سفارش در سیستم مبدا.', 0);


//CREATE TABLE tbl_order_items (
//        id INT PRIMARY KEY IDENTITY(1,1),
//    order_local_id INT NOT NULL,
//    product_woocommerce_id INT NOT NULL,
//    variation_woocommerce_id INT NULL,
//    name NVARCHAR(255) NOT NULL,
//    quantity INT NOT NULL,
//    total DECIMAL(18, 4) NOT NULL,
//    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (order_local_id) REFERENCES tbl_orders(id) ON DELETE CASCADE
//    );
//INSERT INTO tbl_field_documentation (TableName, FieldName, DataType, DescriptionEN, DescriptionFA, IsSystemManaged) VALUES
//    ('tbl_order_items', 'id', 'INT', 'Internal unique identifier for the order item.', N'شناسه یکتای داخلی آیتم سفارش.', 1),
//    ('tbl_order_items', 'order_local_id', 'INT', 'The ID of the order in tbl_orders that this item belongs to.', N'شناسه سفارش در جدول tbl_orders که این آیتم به آن تعلق دارد.', 0),
//    ('tbl_order_items', 'product_woocommerce_id', 'INT', 'The WooCommerce ID of the purchased product.', N'شناسه ووکامرس محصول خریداری شده.', 0),
//    ('tbl_order_items', 'variation_woocommerce_id', 'INT', 'The WooCommerce ID of the purchased variation (if the product is variable).', N'شناسه ووکامرس متغیر خریداری شده (اگر محصول متغیر باشد).', 0),
//    ('tbl_order_items', 'name', 'NVARCHAR(255)', 'The name of the product at the time of purchase.', N'نام محصول در زمان خرید.', 0),
//    ('tbl_order_items', 'quantity', 'INT', 'The quantity of this item purchased.', N'تعداد خریداری شده از این آیتم.', 0),
//    ('tbl_order_items', 'total', 'DECIMAL(18, 4)', 'The total amount for this line item (quantity * unit price).', N'مبلغ کل برای این ردیف آیتم (تعداد * قیمت واحد).', 0);

//--بازگرداندن نمایش پیام تعداد ردیف‌ها
//    SET NOCOUNT OFF;
//END;

# 📁 CoreFile - Modern & High-Performance File Explorer

[![NET Framework](https://img.shields.io/badge/.NET-Modern--WPF-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://microsoft.com/windows)

**CoreFile** یک نرم‌افزار مدیریت فایل مدرن، سریع و سبک بر پایه **WPF** و **C#** است که با هدف جایگزینی یا ارائه جایگزینی زیباتر و بهینه‌تر برای File Explorer ویندوز طراحی شده است. این پروژه با رعایت الگوی **MVVM** و بهره‌گیری از تکنیک‌های پیشرفته **UI Virtualization** و **Pixel-Based Scrolling**، تجربه‌ای روان در مدیریت فایل‌های حجیم و پوشه‌های شلوغ ارائه می‌دهد.

---

## ✨ ویژگی‌های کلیدی (Key Features)

### 🎨 رابط کاربری مدرن و انعطاف‌پذیر
* **پشتیبانی از تم تیره و روشن (Dark / Light Mode):** قابلیت تغییر تم برنامه به‌صورت زنده از طریق منوی تنظیمات.
* **پنل پیش‌نمایش هوشمند (Preview Panel):**
  * **Image Preview:** نمایش سریع تصاویر با کیفیت بالا و لود بهینه.
  * **Text Preview:** قابلیت مشاهده محتوای فایل‌های متنی و کدها.
  * **Media / File Info:** نمایش متاداده، نوع فایل و حجم برای سایر فرمت‌ها.
* **طراحی Responsive:** پنل‌ها و ستون‌های قابل تنظیم مجدد همراه با Splitter انعطاف‌پذیر.

### ⚡ کارایی و سرعت بالا (High Performance)
* **Virtualization کاملاً بهینه:** استفاده از `VirtualizingPanel.Recycling` و تنظیمات اختصاصی Cache برای اسکرول روان ۶۰ فریم بر ثانیه در پوشه‌هایی با هزاران فایل.
* **Pixel Scrolling:** اسکرول پیکسلی نرم و بدون پرش به‌جای اسکرول آیتمی.
* **رندر بهینه تصویر:** استفاده از متدهای بهینه Bitmap scaling برای کاهش مصرف حافظه RAM.

### 🛠 امکانات مدیریت فایل
* **ناوبری کامل (Navigation Bar):** کلیدهای رفت و برگشت، به‌روزرسانی مسیر و نوار آدرس صریح.
* **سایدهای جستجو و درایوها:** دسترسی سریع به درایوهای سیستم (This PC) با آیکون‌های اختصاصی.
* **مرتب‌سازی پیشرفته (Sorting):** قابلیت مرتب‌سازی بر اساس نام، نوع، حجم، تاریخ ساخت و تاریخ آخرین تغییرات.
* **کلیدهای میانبر استاندارد (Shortcuts):** پشتیبانی از `Ctrl+C` (کپی)، `Ctrl+X` (برش)، `Ctrl+V` (جایگذاری) و `Delete` (حذف).
* **منوی راست کلیک (Context Menu):** منوی متنی کامل برای دسترسی سریع به عملیات فایل.
* **نوار وضعیت (Status Bar) و Progress Bar:** نمایش وضعیت برنامه‌ و نوار پیشرفت عملیات‌های سنگین پیش‌زمینه.

---

## ⌨️ کلیدهای میانبر (Keyboard Shortcuts)

| کلید ترکیبی | عملکرد |
| :---: | :--- |
| `Ctrl + C` | کپی فایل/پوشه انتخابی |
| `Ctrl + X` | برش (Cut) فایل/پوشه انتخابی |
| `Ctrl + V` | جایگذاری (Paste) در مسیر جاری |
| `Delete` | انتقال فایل/پوشه انتخابی به سطل زباله / حذف |
| `Double Click` | باز کردن پوشه یا اجرا فایل |

---

## 🛠 تکنولوژی‌ها و معماری (Tech Stack)

* **Language:** C#
* **UI Framework:** Windows Presentation Foundation (WPF)
* **Pattern:** MVVM (Model-View-ViewModel)
* **Markup:** XAML (Optimized Visual Tree)

---

## راه اندازی و اجرا (Getting Started)

### پیش‌نیازها
* **Visual Studio 2026**
* ** .NET 10.0 SDK** (یا بالاتر)

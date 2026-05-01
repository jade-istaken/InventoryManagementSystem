// using System;
// using System.Collections.Generic;

// namespace InventoryManagementSystem
// {
//     class UI
//     {
//         static List<Product> products = new List<Product>();
//         static List<User> users = new List<User>();

//         static void Main()
//         {
//             bool running = true;

//             while (running)
//             {
//                 Console.Clear();
//                 Console.WriteLine("========================================");
//                 Console.WriteLine("   INVENTORY MANAGEMENT SYSTEM");
//                 Console.WriteLine("========================================");
//                 Console.WriteLine();
//                 Console.WriteLine("  1. Product Management");
//                 Console.WriteLine("  2. Account Management");
//                 Console.WriteLine("  3. Exit");
//                 Console.WriteLine();
//                 Console.Write("Select an option: ");

//                 string choice = Console.ReadLine();

//                 switch (choice)
//                 {
//                     case "1":
//                         ProductMenu();
//                         break;
//                     case "2":
//                         AccountMenu();
//                         break;
//                     case "3":
//                         running = false;
//                         break;
//                     default:
//                         Console.WriteLine("Invalid option. Press any key to try again.");
//                         Console.ReadKey();
//                         break;
//                 }
//             }

//             Console.WriteLine("Goodbye!");
//         }

//         // ──────────────────────────────────
//         //  PRODUCT MENU
//         // ──────────────────────────────────
//         static void ProductMenu()
//         {
//             bool inMenu = true;

//             while (inMenu)
//             {
//                 Console.Clear();
//                 Console.WriteLine("── Product Management ──");
//                 Console.WriteLine();
//                 Console.WriteLine("  1. Add Product");
//                 Console.WriteLine("  2. View All Products");
//                 Console.WriteLine("  3. Update Product Quantity");
//                 Console.WriteLine("  4. Update Product Price");
//                 Console.WriteLine("  5. Update Reorder Level");
//                 Console.WriteLine("  6. Remove Product");
//                 Console.WriteLine("  7. Back to Main Menu");
//                 Console.WriteLine();
//                 Console.Write("Select an option: ");

//                 string choice = Console.ReadLine();

//                 switch (choice)
//                 {
//                     case "1":
//                         AddProduct();
//                         break;
//                     case "2":
//                         ViewProducts();
//                         break;
//                     case "3":
//                         UpdateQuantity();
//                         break;
//                     case "4":
//                         UpdatePrice();
//                         break;
//                     case "5":
//                         UpdateReorderLevel();
//                         break;
//                     case "6":
//                         RemoveProduct();
//                         break;
//                     case "7":
//                         inMenu = false;
//                         break;
//                     default:
//                         Console.WriteLine("Invalid option. Press any key to try again.");
//                         Console.ReadKey();
//                         break;
//                 }
//             }
//         }

//         static void AddProduct()
//         {
//             Console.Clear();
//             Console.WriteLine("── Add Product ──");
//             Console.WriteLine();

//             Console.Write("Enter product name: ");
//             string name = Console.ReadLine();

//             if (string.IsNullOrWhiteSpace(name))
//             {
//                 Console.WriteLine("Name cannot be empty. Press any key to go back.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.Write("Category (1 = Raw Material, 2 = Finished): ");
//             string catChoice = Console.ReadLine();

//             Console.Write("Manufacturer (leave blank for SELF): ");
//             string manufacturer = Console.ReadLine();

//             Console.Write("Price: ");
//             double price;
//             if (!double.TryParse(Console.ReadLine(), out price))
//             {
//                 price = 0.00;
//             }

//             Console.Write("Starting quantity: ");
//             int quantity;
//             if (!int.TryParse(Console.ReadLine(), out quantity))
//             {
//                 quantity = 0;
//             }

//             Console.Write("Reorder level: ");
//             int reorderLevel;
//             if (!int.TryParse(Console.ReadLine(), out reorderLevel))
//             {
//                 reorderLevel = 0;
//             }

//             Product product;

//             if (catChoice == "2")
//             {
//                 if (string.IsNullOrWhiteSpace(manufacturer))
//                     product = new FinishedProduct(name, price);
//                 else
//                     product = new FinishedProduct(name, manufacturer, price);
//             }
//             else
//             {
//                 if (string.IsNullOrWhiteSpace(manufacturer))
//                     product = new RawProduct(name, price);
//                 else
//                     product = new RawProduct(name, manufacturer, price);
//             }

//             product.Quantity = quantity;
//             product.ReorderLevel = reorderLevel;

//             products.Add(product);

//             Console.WriteLine();
//             Console.WriteLine("Product added successfully. SKU: " + product.SKU);
//             Console.WriteLine("Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void ViewProducts()
//         {
//             Console.Clear();
//             Console.WriteLine("── All Products ──");
//             Console.WriteLine();

//             if (products.Count == 0)
//             {
//                 Console.WriteLine("No products in inventory.");
//             }
//             else
//             {
//                 for (int i = 0; i < products.Count; i++)
//                 {
//                     Product p = products[i];
//                     string status = p.Quantity <= p.ReorderLevel ? " ** LOW STOCK **" : "";

//                     Console.WriteLine("  [{0}] {1}", i + 1, p.Name);
//                     Console.WriteLine("      SKU:          {0}", p.SKU);
//                     Console.WriteLine("      Category:     {0}", p.Category);
//                     Console.WriteLine("      Price:        ${0:F2}", p.Price);
//                     Console.WriteLine("      Quantity:     {0}", p.Quantity);
//                     Console.WriteLine("      Reorder Lvl:  {0}{1}", p.ReorderLevel, status);
//                     Console.WriteLine("      Manufacturer: {0}", p.Manufacturer);
//                     Console.WriteLine();
//                 }
//             }

//             Console.WriteLine("Press any key to go back.");
//             Console.ReadKey();
//         }

//         static void UpdateQuantity()
//         {
//             Console.Clear();
//             Console.WriteLine("── Update Quantity ──");
//             Console.WriteLine();

//             if (products.Count == 0)
//             {
//                 Console.WriteLine("No products available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListProductNames();

//             Console.Write("Select product number: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > products.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.Write("New quantity: ");
//             int qty;
//             if (!int.TryParse(Console.ReadLine(), out qty))
//             {
//                 Console.WriteLine("Invalid number.");
//                 Console.ReadKey();
//                 return;
//             }

//             products[index - 1].Quantity = qty;
//             Console.WriteLine("Quantity updated. Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void UpdatePrice()
//         {
//             Console.Clear();
//             Console.WriteLine("── Update Price ──");
//             Console.WriteLine();

//             if (products.Count == 0)
//             {
//                 Console.WriteLine("No products available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListProductNames();

//             Console.Write("Select product number: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > products.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.Write("New price: ");
//             double price;
//             if (!double.TryParse(Console.ReadLine(), out price))
//             {
//                 Console.WriteLine("Invalid number.");
//                 Console.ReadKey();
//                 return;
//             }

//             products[index - 1].Price = price;
//             Console.WriteLine("Price updated. Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void UpdateReorderLevel()
//         {
//             Console.Clear();
//             Console.WriteLine("── Update Reorder Level ──");
//             Console.WriteLine();

//             if (products.Count == 0)
//             {
//                 Console.WriteLine("No products available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListProductNames();

//             Console.Write("Select product number: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > products.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.Write("New reorder level: ");
//             int level;
//             if (!int.TryParse(Console.ReadLine(), out level))
//             {
//                 Console.WriteLine("Invalid number.");
//                 Console.ReadKey();
//                 return;
//             }

//             products[index - 1].ReorderLevel = level;
//             Console.WriteLine("Reorder level updated. Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void RemoveProduct()
//         {
//             Console.Clear();
//             Console.WriteLine("── Remove Product ──");
//             Console.WriteLine();

//             if (products.Count == 0)
//             {
//                 Console.WriteLine("No products available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListProductNames();

//             Console.Write("Select product number to remove: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > products.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             string removedName = products[index - 1].Name;
//             products.RemoveAt(index - 1);
//             Console.WriteLine("Removed: " + removedName);
//             Console.WriteLine("Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void ListProductNames()
//         {
//             for (int i = 0; i < products.Count; i++)
//             {
//                 Console.WriteLine("  [{0}] {1} ({2})", i + 1, products[i].Name, products[i].Category);
//             }
//             Console.WriteLine();
//         }

//         // ──────────────────────────────────
//         //  ACCOUNT MENU
//         // ──────────────────────────────────
//         static void AccountMenu()
//         {
//             bool inMenu = true;

//             while (inMenu)
//             {
//                 Console.Clear();
//                 Console.WriteLine("── Account Management ──");
//                 Console.WriteLine();
//                 Console.WriteLine("  1. Add User");
//                 Console.WriteLine("  2. View All Users");
//                 Console.WriteLine("  3. Change User Privilege");
//                 Console.WriteLine("  4. Remove User");
//                 Console.WriteLine("  5. Back to Main Menu");
//                 Console.WriteLine();
//                 Console.Write("Select an option: ");

//                 string choice = Console.ReadLine();

//                 switch (choice)
//                 {
//                     case "1":
//                         AddUser();
//                         break;
//                     case "2":
//                         ViewUsers();
//                         break;
//                     case "3":
//                         ChangePrivilege();
//                         break;
//                     case "4":
//                         RemoveUser();
//                         break;
//                     case "5":
//                         inMenu = false;
//                         break;
//                     default:
//                         Console.WriteLine("Invalid option. Press any key to try again.");
//                         Console.ReadKey();
//                         break;
//                 }
//             }
//         }

//         static void AddUser()
//         {
//             Console.Clear();
//             Console.WriteLine("── Add User ──");
//             Console.WriteLine();

//             Console.Write("First name: ");
//             string firstName = Console.ReadLine();

//             Console.Write("Last name: ");
//             string lastName = Console.ReadLine();

//             if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
//             {
//                 Console.WriteLine("Name fields cannot be empty. Press any key to go back.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.Write("Role (1 = Administrator, 2 = Staff): ");
//             string roleChoice = Console.ReadLine();

//             User user;

//             if (roleChoice == "1")
//             {
//                 user = new Administrator(firstName, lastName);
//             }
//             else
//             {
//                 user = new Staff(firstName, lastName);
//             }

//             users.Add(user);

//             Console.WriteLine();
//             Console.WriteLine("User created. Username: " + user.Username);
//             Console.WriteLine("Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void ViewUsers()
//         {
//             Console.Clear();
//             Console.WriteLine("── All Users ──");
//             Console.WriteLine();

//             if (users.Count == 0)
//             {
//                 Console.WriteLine("No users registered.");
//             }
//             else
//             {
//                 for (int i = 0; i < users.Count; i++)
//                 {
//                     User u = users[i];
//                     Console.WriteLine("  [{0}] {1} {2}", i + 1, u.FirstName, u.LastName);
//                     Console.WriteLine("      Username:  {0}", u.Username);
//                     Console.WriteLine("      Privilege: {0}", u.PrivilegeLevel);
//                     Console.WriteLine();
//                 }
//             }

//             Console.WriteLine("Press any key to go back.");
//             Console.ReadKey();
//         }

//         static void ChangePrivilege()
//         {
//             Console.Clear();
//             Console.WriteLine("── Change Privilege ──");
//             Console.WriteLine();

//             if (users.Count == 0)
//             {
//                 Console.WriteLine("No users available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListUserNames();

//             Console.Write("Select user number: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > users.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             Console.WriteLine("Current privilege: " + users[index - 1].PrivilegeLevel);
//             Console.Write("New privilege (1 = Administrator, 2 = Staff, 3 = Guest): ");
//             string privChoice = Console.ReadLine();

//             switch (privChoice)
//             {
//                 case "1":
//                     users[index - 1].PrivilegeLevel = Privilege.Administrator;
//                     break;
//                 case "2":
//                     users[index - 1].PrivilegeLevel = Privilege.Staff;
//                     break;
//                 case "3":
//                     users[index - 1].PrivilegeLevel = Privilege.Guest;
//                     break;
//                 default:
//                     Console.WriteLine("Invalid choice.");
//                     Console.ReadKey();
//                     return;
//             }

//             Console.WriteLine("Privilege updated. Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void RemoveUser()
//         {
//             Console.Clear();
//             Console.WriteLine("── Remove User ──");
//             Console.WriteLine();

//             if (users.Count == 0)
//             {
//                 Console.WriteLine("No users available.");
//                 Console.ReadKey();
//                 return;
//             }

//             ListUserNames();

//             Console.Write("Select user number to remove: ");
//             int index;
//             if (!int.TryParse(Console.ReadLine(), out index) || index < 1 || index > users.Count)
//             {
//                 Console.WriteLine("Invalid selection.");
//                 Console.ReadKey();
//                 return;
//             }

//             string removedName = users[index - 1].Username;
//             users.RemoveAt(index - 1);
//             Console.WriteLine("Removed: " + removedName);
//             Console.WriteLine("Press any key to continue.");
//             Console.ReadKey();
//         }

//         static void ListUserNames()
//         {
//             for (int i = 0; i < users.Count; i++)
//             {
//                 Console.WriteLine("  [{0}] {1} ({2})", i + 1, users[i].Username, users[i].PrivilegeLevel);
//             }
//             Console.WriteLine();
//         }
//     }
// }

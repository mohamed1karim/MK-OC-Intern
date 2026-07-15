import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { adminGuard } from './guards/admin.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login',
  },
  {
    title: 'Log in',
    path: 'login',
    loadComponent: () => import('../Pages/Pages/login').then((m) => m.Login),
  },
  {
    title: 'Home',
    path: 'home',
    loadComponent: () => import('./home/home').then((m) => m.Home),
    canActivate: [authGuard],
  },
  {
    title: 'Products',
    path: 'products',
    loadComponent: () =>
      import('../Pages/product/product').then((m) => m.ProductsComponent),
    canActivate: [authGuard],
  },
  {
    title: 'Add Product',
    path: 'products/new',
    loadComponent: () =>
      import('../Pages/Pages/add-product/add-product').then((m) => m.AddProduct),
    canActivate: [authGuard],
  },
  {
    title: 'Product Detail',
    path: 'products/:id',
    loadComponent: () =>
      import('../Pages/Pages/product-detail/product-detail').then((m) => m.ProductDetail),
    canActivate: [authGuard],
  },
  {
    title: 'Edit Product',
    path: 'products/:id/edit',
    loadComponent: () =>
      import('../Pages/Pages/edit-product/edit-product').then((m) => m.EditProduct),
    canActivate: [authGuard],
  },
  {
    title: 'Cart',
    path: 'cart',
    loadComponent: () => import('../Pages/Pages/cart/cart').then((m) => m.Cart),
    canActivate: [authGuard],
  },
  {
    title: 'Orders',
    path: 'orders',
    loadComponent: () =>
      import('../Pages/Pages/orders/orders').then((m) => m.Orders),
    canActivate: [authGuard],
  },
  {
    title: 'Order Detail',
    path: 'orders/:id',
    loadComponent: () =>
      import('../Pages/Pages/order-detail/order-detail').then((m) => m.OrderDetail),
    canActivate: [authGuard],
  },
  {
    title: 'Users',
    path: 'users',
    loadComponent: () =>
      import('../Pages/Pages/users/users').then((m) => m.Users),
    canActivate: [authGuard, adminGuard],
  },
  {
    title: 'User Orders',
    path: 'users/:id/orders',
    loadComponent: () =>
      import('../Pages/Pages/user-orders/user-orders').then((m) => m.UserOrders),
    canActivate: [authGuard, adminGuard],
  },
  {
    title: 'Stock Movement History',
    path: 'stock-history',
    loadComponent: () =>
      import('../Pages/Pages/stock-history/stock-history').then((m) => m.StockHistory),
    canActivate: [authGuard, adminGuard],
  },
  {
    title: 'Analytics',
    path: 'analytics',
    loadComponent: () =>
      import('../Pages/Pages/analytics/analytics').then((m) => m.Analytics),
    canActivate: [authGuard, adminGuard],
  },
];

import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Order ids aren't known at build time, so this can't be prerendered like
  // the rest of the app — render it per-request on the server instead.
  {
    path: 'orders/:id',
    renderMode: RenderMode.Server
  },
  {
    path: 'products/:id',
    renderMode: RenderMode.Server
  },
  {
    path: 'products/:id/edit',
    renderMode: RenderMode.Server
  },
  {
    path: 'users/:id/orders',
    renderMode: RenderMode.Server
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];

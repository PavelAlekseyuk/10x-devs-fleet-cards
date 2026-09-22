import { routes } from './app.routes';

describe('application routes', () => {
  it('keeps login public and all shell sections protected', () => {
    expect(routes.find((route) => route.path === 'login')?.canMatch).toBeUndefined();
    expect(routes.filter((route) => ['overview', 'cars', 'cards', 'transactions', 'programs'].includes(route.path ?? '')).every((route) => route.canMatch?.length)).toBe(true);
  });

  it('protects the programs route with the read-write guard', () => {
    const programs = routes.find((route) => route.path === 'programs');
    expect(programs?.canMatch?.length).toBe(2);
  });
});
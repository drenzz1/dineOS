import { render, screen } from '@testing-library/react';

function Hello() {
  return <p>dineOS is alive</p>;
}

test('smoke: renders a component into the document', () => {
  render(<Hello />);
  expect(screen.getByText('dineOS is alive')).toBeInTheDocument();
});

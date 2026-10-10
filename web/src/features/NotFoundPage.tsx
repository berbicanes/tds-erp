import { Link } from "react-router";

export function NotFoundPage() {
  return (
    <div className="space-y-3 px-4 py-6">
      <p className="text-slate-700">Stranica ne postoji.</p>
      <Link to="/" className="text-blue-800 underline">
        Nazad na pregled
      </Link>
    </div>
  );
}

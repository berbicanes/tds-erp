import type { Role } from "../lib/auth";

export interface NavItem {
  to: string;
  label: string;
  /** Visible only to these roles; undefined = everyone. */
  roles?: Role[];
}

/** UI §2 — sidebar order. */
export const mainNav: NavItem[] = [
  { to: "/", label: "Pregled" },
  { to: "/nalozi", label: "Nalozi" },
  { to: "/proizvodnja", label: "Dnevna proizvodnja" },
  { to: "/zalihe", label: "Zalihe" },
  { to: "/kvalitet", label: "Kvalitet" },
  { to: "/dobavljaci", label: "Dobavljači" },
  { to: "/oprema", label: "Oprema" },
  { to: "/dokumenti", label: "Dokumenti" },
  { to: "/podesavanja", label: "Podešavanja", roles: ["Admin"] },
];

/** UI §2 — mobile bottom bar: Pregled · Brzi unos · Nalozi · Zalihe. */
export const bottomNav: NavItem[] = [
  { to: "/", label: "Pregled" },
  { to: "/brzi-unos", label: "Brzi unos" },
  { to: "/nalozi", label: "Nalozi" },
  { to: "/zalihe", label: "Zalihe" },
];

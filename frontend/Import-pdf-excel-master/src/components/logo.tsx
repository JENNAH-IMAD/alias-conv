import Image from 'next/image';
import lightLogo from '../../assets/white mode logo.png';
import darkLogo from '../../assets/dark mode logo.png';

/** SVG containers preserve the original artwork supplied in assets/. */
export function Logo() {
  return <span className="alias-logo">
    <Image src={lightLogo} alt="Alias Convert PDF — Alias Informatique" width={896} height={469} className="alias-logo-light" sizes="(max-width: 850px) 190px, 220px" />
    <Image src={darkLogo} alt="Alias Convert PDF — Alias Informatique" width={896} height={455} className="alias-logo-dark" sizes="(max-width: 850px) 190px, 220px" />
  </span>;
}

using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Xml;

namespace projetBDD
{
    class Program
    {
        /// <summary>
        /// Programme principal
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //On ouvre la connection
            string connectionString = "SERVER=localhost;PORT=3306;DATABASE=ny_crimes;UID=esilvs6;PASSWORD=esilvs6;persistsecurityinfo=True;";
            MySqlConnection connection = new MySqlConnection(connectionString);
            bool continuer = true;
            bool valid = true;
            string lecture = "";

            #region Array menu 
            // Array des descriptions des différentes fonctionnalités
            string[] menu = new string[] { "Importer une journée de crimes" ,
                                           "Exporter le bilan journalier",
                                           "Saisie d'un crime",
                                           "Nombre de crimes par quartier et par catégorie",
                                           "Récapitulatif pour un mois des 'grand larcery' par quartier",
                                           "Evolution mois par mois du % de crimes à New-York par quartier pour les grand larcery",
                                           "Quartier le plus criminogène",
                                           "Fonctionnalité personnelle",
                                           "Quitter le programme..."};
            // Liste des méthodes des différentes fonctionnalités
            List<Func<MySqlConnection, Boolean>> functions = new List<Func<MySqlConnection, Boolean>>();
            functions.Add(Insertion_Journee);
            functions.Add(Bilan_journalier);
            functions.Add(Inserer);
            functions.Add(Nombre_Crime);
            functions.Add(Grand_Larcery);
            functions.Add(Evolution);
            functions.Add(Quartier_Criminogene);
            functions.Add(Perso);
            functions.Add(Quitter);
            #endregion

            //Menu interactif
            //---------------
            do
            {

                Console.WriteLine("choisissez un des programmes suivants : ");
                for (int i = 0; i < menu.Length; i++) { Console.WriteLine("    - " + (i + 1) + " : " + menu[i]); }

                // On demande de choisir un programme qu'on ne peut pas lancer tant que la saisie n'est pas valide
                do
                {
                    lecture = "";
                    valid = true;

                    Console.Write("\nchoisissez un programme > ");
                    lecture = Console.ReadLine();
                    Console.WriteLine(lecture);
                    // Test de la validité de la saisie
                    if (lecture == "" || !"123456789".Contains(lecture[0]))
                    {
                        Console.WriteLine("votre choix <" + lecture + "> n'est pas valide = > recommencez ");
                        valid = false;
                    }
                } while (!valid);


                // On lance le programme
                Console.Clear();
                continuer = functions[Convert.ToInt32(lecture[0]) - 49](connection);
                Console.WriteLine("\n\nTapez sur une touche pour continuer >");
                Console.ReadKey();
                Console.Clear();

            } while (continuer);
        }

        /// <summary>
        ///  Insérer une journée depuis un fichier .xml
        /// </summary>
        /// <param name="connection">connection au SQL</param>
        /// <returns>continuer = true</returns>
        static bool Insertion_Journee(MySqlConnection connection)
        {
            connection.Open();
            List<string> date = new List<string>();
            List<string> data = new List<string>();
            Console.WriteLine("Saisissez le nom du fichier");
            string nomfichier = Console.ReadLine();

            // On récupère les descriptions
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            command.CommandText = @"SELECT description, desc_specificity 
                                    FROM crime_description;";
            reader = command.ExecuteReader();
            List<string[]> tab_description_crime = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[2];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tab_description_crime.Add(tmp);
            }
            connection.Close();

            // On récupère les juridictions
            connection.Open();
            command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM jurisdiction;";
            reader = command.ExecuteReader();
            List<string[]> tab_juridiction = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[1];
                tmp[0] = reader.GetString(0);
                tab_juridiction.Add(tmp);
            }
            connection.Close();

            connection.Open();
            XmlDocument document = new XmlDocument();
            document.Load(nomfichier);
            XmlElement resultat = document.DocumentElement;
            // On retraduit le document XML pour l'importer dans la BDD SQL
            foreach (XmlNode node in resultat)
            {
                // Si c'est le node d'un crime...
                if (node.Name == "crime")
                {
                    data = new List<string>();
                    // ... alors on récupère ses données
                    foreach (XmlNode child in node.ChildNodes) { data.Add(child.InnerText); }

                    // Et on insère la ligne dans la BDD
                    command = connection.CreateCommand();
                    command.CommandText = @"INSERT INTO NY_Crimes.Crime(date, borough, coord_X, coord_Y, crime_description_id, jurisdiction_id) 
                                            VALUES(@date, @borough, 980061, 200006, @idcrime, @idjuri); ";
                    command.Parameters.AddWithValue("@date", date[1] + "/" + date[0] + "/2012");
                    command.Parameters.AddWithValue("@borough", data[0]);
                    command.Parameters.AddWithValue("@idcrime", Capter_description(tab_description_crime, data[1], data[2]));
                    command.Parameters.AddWithValue("@idjuri", Capter_juridiction(tab_juridiction, data[3]));
                    command.ExecuteNonQuery();
                    connection.Close();
                    connection.Open();
                }
                // Si c'est le noeud de la date on la récupère (toujours premier tour de boucle :P )
                else if (node.Name == "date") { foreach (XmlNode child in node.ChildNodes) { date.Add(child.InnerText); } }

            }

            connection.Close();
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection">Connection SQL</param>
        /// <returns>connection = true</returns>
        static bool Bilan_journalier(MySqlConnection connection)
        {
            connection.Open();
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            // On créer le document et sa racine
            XmlDocument document = new XmlDocument();
            XmlElement racine = document.CreateElement("resultats");
            document.AppendChild(racine);

            document.InsertBefore(document.CreateXmlDeclaration("1.0", "UTF-8", "no"), racine);

            Console.WriteLine("Veuillez choisir un jour ainsi qu'un mois sous le format MM/DD");
            string date = Console.ReadLine() + "/2012";
            // On récupère les données pour le jour en question
            command.CommandText = @"SELECT borough, crime_description.description, crime_description.desc_specificity, jurisdiction.name 
                                    FROM crime , crime_description, jurisdiction
                                    WHERE date = '" + date + @"' 
                                    AND jurisdiction.id = crime.jurisdiction_id 
                                    AND crime.crime_description_id = crime_description.id 
                                    ORDER by borough;";
            reader = command.ExecuteReader();

            // liste des noms de variables
            string[] noms_b = new string[] { "mois", "jour", "crime", "borough", "desc_crime", "desc_specificity", "jurisdiction" };

            // On créer la balise date
            List<string> date_list = new List<string>();
            date_list = date.Split('/').ToList<string>();
            XmlElement date_b = document.CreateElement("date");
            for (int i = date_list.Count - 2; i >= 0; i--) { date_b.AppendChild(Creer_b(document, noms_b[i], date_list[i])); }
            racine.AppendChild(date_b);

            // On créer toutes les balises des crimes
            while (reader.Read())
            {
                string[] tmp = new string[4];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tmp[2] = reader.GetString(2);
                tmp[3] = reader.GetString(3);
                XmlElement crime_b = document.CreateElement("crime");
                racine.AppendChild(crime_b);
                for (int i = 0; i < tmp.Length; i++) { crime_b.AppendChild(Creer_b(document, noms_b[i + 3], tmp[i])); }

            }
            // On sauvegarde
            document.Save("NYP_" + date.Replace('/', '_') + ".xml");
            connection.Close();
            return true;
        }
        /// <summary>
        /// Creer une balise XML
        /// </summary>
        /// <param name="document">document</param>
        /// <param name="nom">nom de la balise</param>
        /// <param name="val">valeur de la balise</param>
        /// <returns>balise XMLElementt</returns>
        static XmlElement Creer_b(XmlDocument document, string nom, string val)
        {
            XmlElement b = document.CreateElement(nom);
            b.InnerText = val;
            return b;
        }
        /// <summary>
        /// Insérer un crime
        /// </summary>
        /// <param name="connection">Conncetion SQL</param>
        /// <returns></returns>
        static bool Inserer(MySqlConnection connection)
        {
            connection.Open();
            // On demande les détails du crime
            MySqlCommand command = connection.CreateCommand();
            Console.WriteLine("Veuillez choisir la juridiction");
            // On creer la requete avec des paramètres pour les détails à demander
            command.CommandText = @"INSERT INTO NY_Crimes.Crime (date,borough,coord_X,coord_Y,crime_description_id,jurisdiction_id) 
                                    VALUES(@date, @borough, 123, 456, @idcrime,@idjuri);";


            Console.WriteLine("Veuillez insérer la date du crime sous le format MM/DD/YYYY");
            command.Parameters.AddWithValue("@date", Console.ReadLine());

            Console.WriteLine("Veuillez saisir le quartier concerné, Tapez la lettre correspondante :");
            string[] quartiers = new string[] { "BROOKLYN",
                                                "MANHATTAN",
                                                "BRONX",
                                                "QUEENS",
                                                "STATEN ISLAND"};
            for (int i = 0; i < quartiers.Length; i++) { Console.WriteLine("\t- " + (i + 1) + " : " + quartiers[i]); }
            bool valid = false;
            string lecture;
            do
            {
                lecture = "";
                valid = true;
                lecture = Console.ReadLine();
                if (lecture == "" || !"12345".Contains(lecture[0]))
                {
                    Console.WriteLine("votre choix <" + lecture + "> n'est pas valide = > recommencez ");
                    valid = false;
                }
            } while (!valid);
            command.Parameters.AddWithValue("@borough", quartiers[Convert.ToInt32(lecture[0]) - 49]);


            Console.WriteLine("Veuillez choisir le type de crime (numéro)");
            command.Parameters.AddWithValue("@idcrime", Convert.ToInt32(Console.ReadLine()));

            Console.WriteLine("Veuillez choisir la juridiction (numéro)");
            command.Parameters.AddWithValue("@idjuri", Convert.ToInt32(Console.ReadLine()));

            command.ExecuteNonQuery();
            Console.WriteLine("Done !");
            connection.Close();
            return true;
        }
        /// <summary>
        /// Affiche le nombre de crime pour une journée par quartier et type de crime
        /// </summary>
        /// <param name="connection">connection SQL</param>
        /// <returns>continuer = true</returns>
        static bool Nombre_Crime(MySqlConnection connection)
        {
            connection.Open();
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            Console.WriteLine("Veuillez choisir un jour ainsi qu'un mois sous le format MM/DD");
            string date = Console.ReadLine();
            command.CommandText = @"SELECT borough, crime_description.description, count(*) 
                                    FROM crime, crime_description 
                                    WHERE date = '" + date + @"/2012' AND
                                    crime.crime_description_id = crime_description.id 
                                    GROUP BY borough,crime_description.description 
                                    ORDER BY borough;";
            reader = command.ExecuteReader();
            List<string[]> tab = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[3];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tmp[2] = reader.GetString(2);
                tab.Add(tmp);
            }
            tab.ForEach(line => { foreach (string element in line) { Console.Write(element + " "); } Console.WriteLine(); });

            connection.Close();
            return true;
        }
        /// <summary>
        /// Récapitulatif pour un mois des 'grand larcery' par quartier
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        static bool Grand_Larcery(MySqlConnection connection)
        {
            connection.Open();
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            Console.WriteLine("Taper le mois voulu (entre 1 et 12)");
            string mois = Console.ReadLine();
            if (mois.Length == 1) { mois = "0" + mois; }
            command.CommandText = @"SELECT C.borough, D.desc_specificity, count(*)
                                  FROM crime C, crime_description D
                                  WHERE C.crime_description_id = D.id
                                  AND D.description = 'grand larceny'
                                  AND C.date LIKE '%" + mois + @"/%/2012%'
                                  GROUP BY C.borough, C.crime_description_id
                                  ORDER BY C.borough, C.crime_description_id";
            reader = command.ExecuteReader();
            List<string[]> tab = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[3];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tmp[2] = reader.GetString(2);
                tab.Add(tmp);
            }
            tab.ForEach(line => { foreach (string element in line) { Console.Write(element + " "); } Console.WriteLine(); });
            connection.Close();
            return true;
        }
        /// <summary>
        /// Affiche le pourcentage de crime recensé par quartier et par mois
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        static bool Evolution(MySqlConnection connection)
        {

            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            Console.WriteLine("Evolution par mois du % de crimes à New York par quartier pour les grand larcery");
            for (int i = 1; i < 13; i++)
            {
                connection.Open();
                command.CommandText = @"SELECT borough, count(*) 
                                        FROM crime 
                                        WHERE crime_description_id < 44 
                                        AND date LIKE '%" + i + @"/%/2012%' 
                                        GROUP BY borough;";
                reader = command.ExecuteReader();
                List<string[]> tab = new List<string[]>();
                int total = 0;
                while (reader.Read())
                {
                    string[] tmp = new string[2];
                    tmp[0] = reader.GetString(0);
                    tmp[1] = reader.GetString(1);
                    total += Convert.ToInt32(tmp[1]);
                    tab.Add(tmp);
                }
                tab.ForEach(x => x[1] = Convert.ToString((int)((Convert.ToDouble(x[1]) / total) * 100)));
                Console.WriteLine();
                Console.WriteLine("Pour le mois " + i + " :");
                Console.WriteLine();
                tab.ForEach(line => { foreach (string element in line) { Console.Write(element + " "); } Console.WriteLine('%'); });

                connection.Close();
            }
            return true;
        }
        /// <summary>
        /// Affiche le quartier le plus criminogène et les crimes les plus commis
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        static bool Quartier_Criminogene(MySqlConnection connection)
        {
            connection.Open();
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            command.CommandText = @"SELECT max(total.number), crime.borough 
                                    FROM crime CROSS JOIN (SELECT count(*) AS number, borough 
                                                           FROM crime 
                                                           GROUP BY borough) AS total;";
            reader = command.ExecuteReader();
            List<string[]> tab = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[2];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tab.Add(tmp);
            }
            string borough = tab[0][1];
            Console.WriteLine("Quartier le plus criminogène, avec " + tab[0][0] + " crimes recensés, est " + borough);

            connection.Close();
            connection.Open();
            command.CommandText = @"SELECT crime_description.description, desc_specificity, count(*) 
                                    FROM crime, crime_description
                                    WHERE crime.crime_description_id = crime_description.id 
                                    AND borough='" + borough + @"'
                                    GROUP BY crime_description.desc_specificity 
                                    ORDER BY count(*) DESC;";
            reader = command.ExecuteReader();
            tab = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[3];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(2);
                tab.Add(tmp);
            }
            Console.WriteLine("\nLes 3 crimes les plus commis à " + borough + " sont :");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine("    - " + tab[i][0] + " :  " + tab[i][1]);
            }
            connection.Close();
            return true;
        }
        /// <summary>
        /// Afffiche le minimin/maximum/moyenne/Ecart-type du nombre de crime par jour sur 2012
        /// </summary>
        /// <param name="connection">connection SQL</param>
        /// <returns>continuer = true</returns>
        static bool Perso(MySqlConnection connection)
        {
            connection.Open();
            MySqlCommand command = connection.CreateCommand();
            MySqlDataReader reader;
            command.CommandText = @"SELECT count(*) as c, date 
                                    FROM crime 
                                    GROUP BY date 
                                    ORDER BY c DESC;";
            reader = command.ExecuteReader();
            List<string[]> tab = new List<string[]>();
            while (reader.Read())
            {
                string[] tmp = new string[2];
                tmp[0] = reader.GetString(0);
                tmp[1] = reader.GetString(1);
                tab.Add(tmp);
            }
            Console.WriteLine("Sur l'année 2012 :");
            Console.WriteLine("\t- Le jour où il y a eu le plus de crimes (" + tab[0][0] + ") est le " + tab[0][1]);
            Console.WriteLine("\t- Le jour où il y a eu le moins de crimes (" + tab[tab.Count - 1][0] + ") est le " + tab[tab.Count - 1][1]);
            List<int> crime_par_jour = new List<int>();
            tab.ForEach(x => crime_par_jour.Add(Convert.ToInt32(x[0])));

            int moyenne = 0;
            crime_par_jour.ForEach(x => moyenne += x);
            moyenne /= crime_par_jour.Count;

            int variance = 0;
            crime_par_jour.ForEach(x => variance += (x - moyenne) * (x - moyenne));
            variance /= crime_par_jour.Count;

            Console.WriteLine("\t- Chaque jour il y a en moyenne " + moyenne + " crimes plus ou moins " + (int)Math.Pow(variance, 0.5));


            connection.Close();
            return true;
        }
        /// <summary>
        /// Retourne l'id du crime en question
        /// </summary>
        /// <param name="tab">tableau de crimes</param>
        /// <param name="description">description à chercher</param>
        /// <param name="specificity">specificité à cherhcer</param>
        /// <returns>id du crime</returns>
        static int Capter_description(List<string[]> tab, string description, string specificity)
        {
            int count = 0;
            foreach (string[] element in tab)
            {

                count++;

                if (element[0] == description && element[1] == specificity)
                {
                    return count;
                }
            }
            return -1;
        }
        /// <summary>
        /// Retourne l'id de la juridiction en question
        /// </summary>
        /// <param name="tab">tableau de juridiction</param>
        /// <param name="juridiction">juridiction à chercher</param>
        /// <returns>id de la juridiction</returns>
        static int Capter_juridiction(List<string[]> tab, string juridiction)
        {
            int count = 0;
            foreach (string[] element in tab)
            {
                count++;
                if (element[0] == juridiction)
                {
                    return count;
                }
            }
            return -1;
        }
        /// <summary>
        /// Indique que le programme ne continuera pas 
        /// </summary>
        /// <param name="connection">connection SQL</param>
        /// <returns>continuer = false</returns>
        static bool Quitter(MySqlConnection connection)
        {
            Console.Clear();
            Console.WriteLine("Il va faire tout noir ! ");
            return false;
        }
    } 
}


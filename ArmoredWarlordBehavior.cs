using UnityEngine.UI;

public class ArmoredWarlordBehavior : AttackerSandbox {

	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////


	//armored warlord stats
	private int armoredSpeed = 1;
	private int armoredAttack = 1;
	private int armoredArmor = 2;
	private int armoredHealth = 4;
	private const string NAME = "Armored Warlord";


	//UI for Armored Warlord health
	private Image healthUI;
	private const string HEALTH_CANVAS = "Health canvas";
	private const string HEALTH_IMAGE = "Health image";


	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//initialize variables, including the Armored Warlord's stats
	public override void Setup (){
		base.Setup ();
		speed = armoredSpeed;
		AttackMod = armoredAttack;
		Armor = armoredArmor;
		Health = armoredHealth;
		healthUI = transform.Find(HEALTH_CANVAS).Find(HEALTH_IMAGE).GetComponent<Image>();
		attackerName = NAME;
	}


	/// <summary>
	/// In addition to normal damage effects, update the Armored Warlord's health UI.
	/// </summary>
	/// <param name="damage">The amount of damage sustained, after all modifiers.</param>
	public override void TakeDamage (int damage){
		base.TakeDamage(damage);

		healthUI.fillAmount = (float)Health/(float)armoredHealth;
	}
}
